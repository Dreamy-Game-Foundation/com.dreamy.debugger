"""Interactive local Telegram relay launcher. Never persists or prints the bot token."""
import argparse
import getpass
import urllib.error
import urllib.parse
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import json
import urllib.request
import unicodedata


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


def read_bot_token():
    candidate = os.getenv('TELEGRAM_BOT_TOKEN')
    while True:
        if candidate is None:
            candidate = getpass.getpass('BotFather HTTP API token (hidden; paste with Ctrl+Shift+V): ')
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


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--settings', help='Editor settings .asset containing ChatId only')
    parser.add_argument('--chat-id')
    parser.add_argument('--discover-chat', action='store_true', help='Read bot identity/recent chat IDs; sends no report')
    args = parser.parse_args()
    token = read_bot_token()
    identity = bot_read(token, 'getMe')
    print('Bot verified: @' + identity.get('username', '(unnamed)'))
    if args.discover_chat:
        chats = {}
        for update in bot_read(token, 'getUpdates?timeout=0&limit=100'):
            for key in ('message', 'edited_message', 'channel_post', 'edited_channel_post', 'my_chat_member'):
                chat = update.get(key, {}).get('chat', {})
                if 'id' in chat:
                    chats[chat['id']] = chat.get('title') or chat.get('username') or chat.get('first_name', '')
        for chat_id, name in chats.items():
            print('Chat ID:', chat_id, '|', name)
        if not chats:
            print('No recent chats. Send /start to the bot privately, or add it to your group and send /start@BotUsername; then retry.')
        print('Discovery only: no report sent, no webhook changed.')
        input('Copy the desired chat ID into the Unity settings, then press Enter to close...')
        return
    chat = args.chat_id or (read_chat_setting(args.settings) if args.settings else '') or os.getenv('TELEGRAM_CHAT_ID') or input('Destination chat ID or @channel: ').strip()
    if not re.fullmatch(r'-?[0-9]+|@[A-Za-z0-9_]+', chat):
        raise SystemExit('Invalid destination format; no connection attempted.')
    destination = bot_read(token, 'getChat?chat_id=' + urllib.parse.quote(chat))
    print('Destination verified:', destination['id'])
    env = os.environ.copy()
    env.update(TELEGRAM_BOT_TOKEN=token, TELEGRAM_CHAT_ID=chat,
               DREAMY_DESTINATIONS='telegram', DREAMY_FAKE_DELIVERY='0',
               DREAMY_BIND='127.0.0.1')
    env.setdefault('DREAMY_RELAY_DB', './data/telegram-local.sqlite')
    env.setdefault('DREAMY_PORT', '8765')
    print('Starting local Telegram relay. Gmail remains disabled. Token is not saved.')
    print('Editor Relay URL: http://127.0.0.1:' + env['DREAMY_PORT'] + '/reports')
    print('Reports submitted to this relay will be sent to the configured chat.')
    directory = Path(__file__).resolve().parent
    data = directory / 'data'
    data.mkdir(exist_ok=True)
    with (data / 'telegram-local.log').open('ab') as log:
        process = subprocess.Popen([sys.executable, str(directory / 'server.py')],
            cwd=directory, env=env, start_new_session=True,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log)
    for _ in range(30):
        if process.poll() is not None:
            raise SystemExit('Relay failed to start. Check Tools~/relay/data/telegram-local.log (do not share credentials).')
        try:
            with urllib.request.urlopen('http://127.0.0.1:' + env['DREAMY_PORT'] + '/health', timeout=1) as response:
                ready = json.load(response)
            if ready.get('service') == 'dreamy.debugger' and ready.get('fakeDelivery') is False:
                (data / 'telegram-local.pid').write_text(str(process.pid))
                print('Relay ready in background. You may close this terminal. PID:', process.pid)
                return
        except (OSError, ValueError):
            time.sleep(.1)
    process.terminate()
    raise SystemExit('Relay readiness timed out; process stopped.')


if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        print('\nSetup canceled.')
    except SystemExit as error:
        if error.code in (None, 0):
            raise
        print(error)
        input('Setup failed. Press Enter to close this window...')
