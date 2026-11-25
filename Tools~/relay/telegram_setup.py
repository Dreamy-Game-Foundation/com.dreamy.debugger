"""Interactive local Telegram relay launcher. Never persists or prints the bot token."""
import argparse
import getpass
import hashlib
import urllib.error
import urllib.parse
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import tempfile
import json
import urllib.request
import unicodedata
import warnings

from server import DISCORD_USER_AGENT, DISCORD_WEBHOOK


def normalize_bot_token(raw):
    value = ''.join(c for c in raw if unicodedata.category(c) != 'Cf').strip()
    if value.startswith('TELEGRAM_BOT_TOKEN='):
        value = value.split('=', 1)[1].strip()
    value = value.strip('"\'“”‘’').strip()
    if value.startswith('bot'):
        value = value[3:]
    if re.fullmatch(r'[0-9]+:[A-Za-z0-9_-]+', value):
        return value
    if not value or any(ord(c) < 32 for c in value):
        raise ValueError('No usable token pasted. Use right-click Paste or Ctrl+Shift+V in this terminal.')
    if ':' not in value:
        raise ValueError('Missing colon (:). Use the full HTTP API token from @BotFather, not chat ID, bot ID, username, api_id or api_hash.')
    raise ValueError('Invalid token characters. Copy only the HTTP API token from @BotFather (digits:letters_digits_-).')


def hidden_input(prompt):
    try:
        with warnings.catch_warnings():
            warnings.simplefilter('error', getpass.GetPassWarning)
            return getpass.getpass(prompt)
    except getpass.GetPassWarning:
        raise SystemExit('Hidden input requires an interactive terminal.') from None


def read_bot_token(force_prompt=False):
    candidate = None if force_prompt else os.getenv('TELEGRAM_BOT_TOKEN')
    while True:
        if candidate is None:
            candidate = hidden_input('BotFather HTTP API token (hidden; paste with Ctrl+Shift+V): ')
        try:
            return normalize_bot_token(candidate)
        except ValueError as error:
            print(str(error) + ' No connection attempted; try again or Ctrl+C to cancel.')
            candidate = None


def read_chat_setting(path):
    match = re.search(r'^[ \t]*ChatId:[ \t]*(.*?)[ \t]*$', Path(path).read_text(), re.MULTILINE)
    if not match:
        return ''
    value = match.group(1)
    if value.startswith('"'):
        value = json.loads(value)
    elif value.startswith("'") and value.endswith("'"):
        value = value[1:-1]
    return value.strip()


def bot_read(token, method):
    try:
        with urllib.request.urlopen('https://api.telegram.org/bot' + token + '/' + method, timeout=15) as response:
            result = json.load(response)
        if not result.get('ok'):
            raise SystemExit('Telegram rejected this read request. Check the bot token.')
        return result['result']
    except urllib.error.HTTPError as error:
        error.close()
        if error.code == 401:
            raise SystemExit('Bot token rejected (401). Copy the HTTP API token from @BotFather, not the bot ID or username.') from None
        if error.code == 409:
            raise SystemExit('Updates unavailable (409): another poller/webhook may own this bot. No webhook was changed.') from None
        raise SystemExit('Telegram read failed: HTTP ' + str(error.code)) from None
    except (OSError, ValueError):
        raise SystemExit('Telegram connection/read failed. Check network; token was not printed.') from None


KEYRING_VERSION = '25.6.0'
KEYRING_SERVICE = 'Dreamy Debugger Telegram Relay'


def credential_backend():
    # Choose a native backend explicitly so configured plaintext backends are never selected.
    try:
        if sys.platform == 'win32':
            from keyring.backends.Windows import WinVaultKeyring as Backend
        elif sys.platform == 'darwin':
            from keyring.backends.macOS import Keyring as Backend
        else:
            from keyring.backends.SecretService import Keyring as Backend
        if Backend.priority <= 0:
            raise RuntimeError()
        return Backend()
    except Exception:
        raise SystemExit('System credential store unavailable. Unlock it, or use manual setup.') from None


def saved_token(profile, action, token=None, discord=False):
    backend = credential_backend()
    service = 'Dreamy Debugger Discord Relay' if discord else KEYRING_SERVICE
    try:
        if action == 'save':
            backend.set_password(service, profile, token)
        elif action == 'forget':
            if backend.get_password(service, profile) is not None:
                backend.delete_password(service, profile)
        else:
            value = backend.get_password(service, profile)
            if value is None:
                raise SystemExit('No saved credential. Enable the remember option and run setup again.')
            return normalize_webhook(value) if discord else normalize_bot_token(value)
    except Exception:
        raise SystemExit('Could not access the system credential store. Unlock it and retry.') from None


def project_python(data):
    root = data / 'credential-env'
    return root / ('Scripts/python.exe' if os.name == 'nt' else 'bin/python')


def prepare_credential_python(data):
    python = project_python(data)
    marker = data / 'credential-env' / ('keyring-' + KEYRING_VERSION)
    if not python.is_file() or not marker.is_file():
        print('Installing credential support in this project…', flush=True)
        try:
            if not python.is_file():
                import venv
                venv.EnvBuilder(with_pip=True).create(str(python.parent.parent))
            subprocess.run([str(python), '-m', 'pip', '--disable-pip-version-check', 'install',
                            '--quiet', 'keyring==' + KEYRING_VERSION], check=True, timeout=180)
            marker.touch()
        except (ImportError, OSError, subprocess.SubprocessError, RuntimeError):
            raise SystemExit('Credential support could not be installed. Check Python venv/pip and retry setup.') from None
    return python


def write_chat(data, chat, filename='chat-id'):
    # Keep the local choice across Unity restarts. The token only lives in the credential store.
    with tempfile.NamedTemporaryFile(mode='w', dir=data, delete=False) as stream:
        stream.write(chat)
        temporary = Path(stream.name)
    try:
        temporary.replace(data / filename)
    finally:
        temporary.unlink(missing_ok=True)


def fingerprint(value):
    return hashlib.sha256(value.encode('utf-8')).hexdigest()


def read_health(url):
    try:
        with urllib.request.urlopen(url + '/health', timeout=1) as response:
            return json.loads(response.read(4096))
    except (OSError, ValueError):
        return None


def matches_relay(health, relay_id, destination='telegram'):
    return isinstance(health, dict) and health.get('service') == 'dreamy.debugger' and \
        health.get('fakeDelivery') is False and health.get('local' + destination.title() + 'Profile') == relay_id


def start_relay(token, chat, data, directory, relay_id='', port='', reuse=False, destination='telegram'):
    env = os.environ.copy()
    env.pop('DREAMY_LOCAL_TELEGRAM_PROFILE', None)
    env.pop('DREAMY_LOCAL_DISCORD_PROFILE', None)
    env.update(DREAMY_DESTINATIONS=destination, DREAMY_FAKE_DELIVERY='0', DREAMY_BIND='127.0.0.1')
    if destination == 'discord':
        env['DREAMY_DISCORD_WEBHOOK'] = token
        env.pop('TELEGRAM_BOT_TOKEN', None)
    else:
        env.update(TELEGRAM_BOT_TOKEN=token, TELEGRAM_CHAT_ID=chat)
        env.pop('DREAMY_DISCORD_WEBHOOK', None)
    if relay_id:
        env['DREAMY_LOCAL_' + destination.upper() + '_PROFILE'] = relay_id
        env.update(DREAMY_PORT=port, DREAMY_RELAY_DB=str(data / (destination + '-' + relay_id + '.sqlite')))
        # A local project launcher uses the same endpoint as the Editor's default local setup.
        env.pop('DREAMY_INGEST_KEY', None)
    else:
        env.setdefault('DREAMY_RELAY_DB', './data/' + destination + '-local.sqlite')
        env.setdefault('DREAMY_PORT', port)
    url = 'http://127.0.0.1:' + env['DREAMY_PORT']
    previous = read_health(url)
    if previous is not None:
        if reuse and matches_relay(previous, relay_id, destination):
            print(destination.title() + ' relay already running. Editor Relay URL: ' + url + '/reports')
            return
        raise SystemExit('Relay port already in use. Stop the other relay or choose a different port.')

    detached = {'creationflags': subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == 'nt' else {'start_new_session': True}
    data.mkdir(parents=True, exist_ok=True)
    with (data / (destination + '-local.log')).open('ab') as log:
        process = subprocess.Popen([sys.executable, str(directory / 'server.py')], cwd=directory, env=env,
                                   stdin=subprocess.DEVNULL, stdout=log, stderr=log, **detached)
    for _ in range(30):
        if process.poll() is not None:
            if relay_id and matches_relay(read_health(url), relay_id, destination):
                print(destination.title() + ' relay already running. Editor Relay URL: ' + url + '/reports')
                return
            raise SystemExit('Relay failed to start. Check ' + destination + '-local.log or change the local port.')
        health = read_health(url)
        if isinstance(health, dict) and health.get('pid') == process.pid and matches_relay(health, relay_id or None, destination):
            (data / (destination + '-local.pid')).write_text(str(process.pid))
            print('Relay ready in background. Editor Relay URL: ' + url + '/reports')
            return
        time.sleep(.1)
    if process.poll() is None:
        process.terminate()
    raise SystemExit('Relay readiness timed out; process stopped.')

def normalize_webhook(value):
    webhook = value.strip().strip('"\'')
    if not DISCORD_WEBHOOK.fullmatch(webhook):
        raise ValueError('Use the complete Discord webhook URL from the channel settings.')
    return webhook


def verify_discord_webhook(webhook):
    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, *args, **kwargs):
            return None
    try:
        request = urllib.request.Request(webhook, headers={'User-Agent': DISCORD_USER_AGENT})
        with urllib.request.build_opener(NoRedirect()).open(request, timeout=15) as response:
            result = json.loads(response.read(65536))
        if not isinstance(result, dict) or result.get('type') != 1:
            raise ValueError()
        channel = result.get('channel_id')
        if not isinstance(channel, str) or not channel.isascii() or not channel.isdigit():
            raise ValueError()
        return channel
    except urllib.error.HTTPError as error:
        error.close()
        raise SystemExit('Discord webhook verification failed (HTTP ' + str(error.code) + '). Check the webhook URL.') from None
    except (OSError, ValueError):
        raise SystemExit('Could not verify the Discord webhook. Check its channel and your connection.') from None


def discord_setup(args, data, directory, profile):
    if args.forget_token:
        saved_token(profile, 'forget', discord=True)
        print('Saved Discord webhook removed. A running relay keeps it until stopped.')
        return
    if args.start_saved:
        metadata = data / 'destination-id'
        if not metadata.is_file():
            raise SystemExit('No saved Discord destination. Run setup with Remember webhook enabled.')
        relay_id = metadata.read_text().strip()
        if not re.fullmatch(r'[a-f0-9]{64}', relay_id):
            raise SystemExit('Saved Discord destination is invalid. Run setup again.')
        url = 'http://127.0.0.1:' + args.port
        if matches_relay(read_health(url), relay_id, 'discord'):
            print('Discord relay already running. Editor Relay URL: ' + url + '/reports')
            return
        webhook = saved_token(profile, 'load', discord=True)
    else:
        try:
            webhook = normalize_webhook(hidden_input('Discord webhook URL (hidden): '))
        except ValueError as error:
            raise SystemExit(str(error)) from None
        channel = verify_discord_webhook(webhook)
        # A different webhook or channel must not reuse another destination's pending database.
        webhook_id = webhook.rsplit('/', 2)[-2]
        relay_id = fingerprint(profile + '/discord/' + channel + '/' + webhook_id)
        if args.remember_token:
            saved_token(profile, 'save', webhook, discord=True)
            write_chat(data, relay_id, 'destination-id')
            print('Discord webhook saved in the system credential store.')
    start_relay(webhook, '', data, directory, relay_id, args.port, reuse=True, destination='discord')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--settings', help='Editor settings .asset containing ChatId only')
    parser.add_argument('--chat-id')
    parser.add_argument('--prompt-token', action='store_true', help='Always enter the token at the hidden prompt')
    parser.add_argument('--discover-chat', action='store_true', help='Read bot identity/recent chat IDs; sends no report')
    parser.add_argument('--project-dir', type=Path, help='Unity project directory for local relay settings')
    parser.add_argument('--destination', choices=('telegram', 'discord'), default='telegram')
    parser.add_argument('--port')
    parser.add_argument('--remember-token', action='store_true')
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument('--start-saved', action='store_true')
    modes.add_argument('--forget-token', action='store_true')
    args = parser.parse_args()
    directory = Path(__file__).resolve().parent
    project = args.project_dir.resolve() if args.project_dir else None
    discord = args.destination == 'discord'
    args.port = args.port or ('8766' if discord else '8765')
    managed = args.remember_token or args.start_saved or args.forget_token
    if (managed or discord) and project is None:
        raise SystemExit('Unity project directory is required for saved credentials.')
    if not re.fullmatch(r'[0-9]{1,5}', args.port) or not 1 <= int(args.port) <= 65535:
        raise SystemExit('Invalid local relay port.')
    data = project / 'Library' / 'DreamyDebugger' / (args.destination.title() + 'Relay') if project else directory / 'data'
    profile = fingerprint(os.path.normcase(str(project))) if project else ''
    if managed and sys.version_info < (3, 9):
        raise SystemExit('Saved relay setup requires Python 3.9 or later.')
    if args.remember_token:
        python = prepare_credential_python(data)
        if Path(sys.executable).absolute() != python.absolute():
            return subprocess.call([str(python), str(Path(__file__).resolve()), *sys.argv[1:]])
    if discord:
        return discord_setup(args, data, directory, profile)
    if args.forget_token:
        saved_token(profile, 'forget')
        print('Saved Telegram token removed. A running relay keeps its current credentials until stopped.')
        return

    chat = args.chat_id or (read_chat_setting(args.settings) if args.settings else '')
    if args.start_saved and not chat and (data / 'chat-id').is_file():
        chat = (data / 'chat-id').read_text().strip()
    if args.start_saved:
        if not re.fullmatch(r'-?[0-9]+|@[A-Za-z0-9_]+', chat or ''):
            raise SystemExit('No saved Telegram destination. Run interactive setup first.')
        relay_id = fingerprint(profile + '/' + chat)
        if matches_relay(read_health('http://127.0.0.1:' + args.port), relay_id):
            print('Telegram relay already running. Editor Relay URL: http://127.0.0.1:' + args.port + '/reports')
            return
        start_relay(saved_token(profile, 'load'), chat, data, directory, relay_id, args.port, reuse=True)
        return

    token = read_bot_token(force_prompt=args.prompt_token or args.remember_token)
    identity = bot_read(token, 'getMe')
    print('Bot verified: @' + identity.get('username', '(unnamed)'))
    if args.discover_chat:
        chats = {}
        for update in bot_read(token, 'getUpdates?timeout=0&limit=100'):
            for key in ('message', 'edited_message', 'channel_post', 'edited_channel_post', 'my_chat_member'):
                found = update.get(key, {}).get('chat', {})
                if 'id' in found:
                    chats[found['id']] = found.get('title') or found.get('username') or found.get('first_name', '')
        for chat_id, name in chats.items():
            print('Chat ID:', chat_id, '|', name)
        if not chats:
            print('No recent chats. Send /start to the bot privately, or add it to your group and send /start@BotUsername; then retry.')
        print('Discovery only: no report sent, no webhook changed.')
        input('Copy the desired chat ID into the Unity settings, then press Enter to close...')
        return
    chat = chat or os.getenv('TELEGRAM_CHAT_ID') or input('Destination chat ID or @channel: ').strip()
    if not re.fullmatch(r'-?[0-9]+|@[A-Za-z0-9_]+', chat):
        raise SystemExit('Invalid destination format; no connection attempted.')
    destination = bot_read(token, 'getChat?chat_id=' + urllib.parse.quote(chat))
    print('Destination verified:', destination['id'])
    if args.remember_token:
        saved_token(profile, 'save', token)
        write_chat(data, chat)
        print('Bot token saved in the system credential store.')
    relay_id = fingerprint(profile + '/' + chat) if project else ''
    start_relay(token, chat, data, directory, relay_id, args.port, reuse=bool(project))


if __name__ == '__main__':
    unattended = '--start-saved' in sys.argv or '--forget-token' in sys.argv
    try:
        status = main()
        if status:
            sys.exit(status)
    except KeyboardInterrupt:
        print('\nSetup canceled.')
    except (SystemExit, OSError) as error:
        if isinstance(error, SystemExit) and isinstance(error.code, (type(None), int)):
            raise
        print(str(error) if isinstance(error, SystemExit) else 'Could not access the local relay environment.')
        if not unattended and sys.stdin.isatty():
            input('Setup failed. Press Enter to close this window...')
        sys.exit(1)
