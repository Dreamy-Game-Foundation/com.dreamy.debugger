import contextlib
import io
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import urllib.error
import telegram_setup as setup

class SetupTests(unittest.TestCase):
    def test_wizard_generates_key_and_uses_saved_destination(self):
        with tempfile.TemporaryDirectory() as folder:
            data = Path(folder)
            (data / 'chat-id').write_text('-42')
            args = SimpleNamespace(destination='telegram', port='8765', settings=None)
            with patch('sys.stdin', io.StringIO('{}')), patch.object(setup, 'saved_token', return_value='fixture') as vault, \
                 patch.object(setup, 'read_health', return_value=None), patch.object(setup, 'start_relay') as start:
                setup.wizard_setup(args, data, data, 'project')
            self.assertRegex(setup.read_ingest_key(data), r'^[0-9a-f]{64}$')
            vault.assert_called_once_with('project', 'load', discord=False)
            start.assert_called_once_with('fixture', '-42', data, data, setup.fingerprint('project/-42'), '8765', reuse=True, destination='telegram')

    def test_wizard_leaves_unowned_relay_running(self):
        with tempfile.TemporaryDirectory() as folder:
            data = Path(folder)
            (data / 'chat-id').write_text('-42')
            args = SimpleNamespace(destination='telegram', port='8765', settings=None)
            with patch('sys.stdin', io.StringIO('{}')), patch.object(setup, 'saved_token', return_value='fixture'), \
                 patch.object(setup, 'read_health', return_value={'service': 'another-service'}), \
                 patch.object(setup.os, 'kill') as kill, patch.object(setup, 'start_relay') as start:
                with self.assertRaisesRegex(SystemExit, 'No process was stopped'):
                    setup.wizard_setup(args, data, data, 'project')
            start.assert_not_called()
            kill.assert_not_called()
            self.assertFalse((data / 'ingest-key').exists())

    def test_saved_profile_restores_from_project_store_without_network(self):
        with tempfile.TemporaryDirectory() as folder:
            project = Path(folder)
            data = project / 'Library/DreamyDebugger/TelegramRelay'
            data.mkdir(parents=True)
            (data / 'chat-id').write_text('-100123')
            profile = setup.fingerprint(os.path.normcase(str(project.resolve())))
            for provider, preset in (
                ('telegram', {'provider': 1, 'credential': '123:FIXTURE', 'chatId': '-100123'}),
                ('discord', {'provider': 0, 'credential': 'fixture-webhook', 'chatId': ''}),
            ):
                argv = ['setup', '--read-saved-profile', '--project-dir', folder, '--destination', provider]
                with patch('sys.argv', argv), patch.object(setup, 'saved_token', return_value=preset['credential']) as read, \
                     patch.object(setup, 'start_relay') as start, patch.object(setup, 'bot_read') as telegram, \
                     patch.object(setup, 'verify_discord_webhook') as discord, contextlib.redirect_stdout(io.StringIO()) as out:
                    setup.main()
                    self.assertEqual(json.loads(out.getvalue()), preset)
                    read.assert_called_once_with(profile, 'load', discord=provider == 'discord')
                    start.assert_not_called()
                    telegram.assert_not_called()
                    discord.assert_not_called()

    def test_saved_profile_rejects_interactive_output(self):
        with tempfile.TemporaryDirectory() as folder, patch('sys.argv', ['setup', '--read-saved-profile', '--project-dir', folder]), \
             patch.object(setup.sys.stdout, 'isatty', return_value=True), patch.object(setup, 'saved_token') as read:
            with self.assertRaisesRegex(SystemExit, 'Use the Direct QA setup'):
                setup.main()
            read.assert_not_called()

    def test_stop_saved_relay_requires_both_live_identity_and_owned_pid(self):
        with tempfile.TemporaryDirectory() as folder:
            data = Path(folder)
            pid_file = data / 'telegram-local.pid'
            pid_file.write_text('12345')
            health = dict(service='dreamy.debugger', fakeDelivery=False, localTelegramProfile='mine', pid=99999)
            with patch.object(setup, 'read_health', return_value=health), patch.object(setup.os, 'kill') as kill:
                with self.assertRaisesRegex(SystemExit, 'No process was stopped'):
                    setup.stop_saved_relay(data, '8765', 'telegram', 'mine')
                kill.assert_not_called()
                health['pid'] = 12345
                setup.stop_saved_relay(data, '8765', 'telegram', 'mine')
                kill.assert_called_once_with(12345, setup.signal.SIGTERM)
                self.assertFalse(pid_file.exists())
    def test_https_ingest_key_file_excludes_provider_credentials(self):
        with tempfile.TemporaryDirectory() as folder:
            data = Path(folder)
            self.assertEqual(setup.read_ingest_key(data), '')
            (data / 'ingest-key').write_text('qa-limited-' + 'a' * 32)
            self.assertEqual(setup.read_ingest_key(data), 'qa-limited-' + 'a' * 32)
            for invalid in ('short', 'contains white space', '123456789:' + 'a' * 35,
                            'https://discord.com/api/webhooks/123/' + 'a' * 35):
                (data / 'ingest-key').write_text(invalid)
                with self.assertRaisesRegex(SystemExit, 'Invalid local ingest key'):
                    setup.read_ingest_key(data)

    def test_https_setup_rejects_unprotected_old_relay(self):
        health = dict(service='dreamy.debugger', fakeDelivery=False, localTelegramProfile='mine')
        self.assertTrue(setup.matches_relay(health, 'mine'))
        self.assertFalse(setup.matches_relay(health, 'mine', require_key=True))
        health['ingestKeyRequired'] = True
        self.assertTrue(setup.matches_relay(health, 'mine', require_key=True))
    def test_token_copy_wrappers_are_normalized(self):
        for raw in [' 123:TEST_key-1 ', '"123:TEST_key-1"', 'bot123:TEST_key-1', '\u200b123:TEST_key-1\u200b', "TELEGRAM_BOT_TOKEN='123:TEST_key-1'"]:
            self.assertEqual(setup.normalize_bot_token(raw), '123:TEST_key-1')

    def test_wrong_identifiers_and_empty_input_are_explained_without_echo(self):
        for raw in ['', '\x16', '-100987', 'my_bot', 'https://t.me/+PRIVATE', '123:BAD key']:
            with self.assertRaises(ValueError) as result:
                setup.normalize_bot_token(raw)
            if raw:
                self.assertNotIn(raw, str(result.exception))

    def test_invalid_input_can_retry_privately(self):
        with patch.dict(os.environ, {'TELEGRAM_BOT_TOKEN': 'not_a_token'}), patch.object(setup.getpass, 'getpass', return_value='123:TEST_TOKEN'), contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(setup.read_bot_token(), '123:TEST_TOKEN')
            self.assertNotIn('not_a_token', output.getvalue())
            self.assertNotIn('TEST_TOKEN', output.getvalue())
    def test_editor_setup_always_prompts_and_requires_hidden_input(self):
        with patch.dict(os.environ, {'TELEGRAM_BOT_TOKEN': '123:ENV_FIXTURE'}):
            with patch.object(setup.getpass, 'getpass', return_value='456:PROMPT_FIXTURE') as prompt:
                self.assertEqual(setup.read_bot_token(force_prompt=True), '456:PROMPT_FIXTURE')
                prompt.assert_called_once()
            with patch.object(setup.getpass, 'getpass', side_effect=setup.getpass.GetPassWarning):
                with self.assertRaisesRegex(SystemExit, 'interactive terminal'):
                    setup.read_bot_token(force_prompt=True)

    def test_editor_asset_id_formats(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'settings.asset'
            for text, expected in [('  ChatId: -100123\n', '-100123'), ('  ChatId: "12345"\n', '12345'), ("  ChatId: '@my_channel'\n", '@my_channel'), ('  ChatId: \n  Other: hello\n', '')]:
                path.write_text(text)
                self.assertEqual(setup.read_chat_setting(path), expected)

    def test_discovery_only_reads_and_never_launches_relay(self):
        with patch.dict(os.environ, {'TELEGRAM_BOT_TOKEN': '123:TEST_TOKEN'}), patch('sys.argv', ['setup', '--discover-chat']), patch.object(setup, 'bot_read', side_effect=[{'username': 'testbot'}, [{'message': {'chat': {'id': -100123, 'title': 'QA'}}}]]), patch.object(setup.subprocess, 'Popen') as launch, patch('builtins.input', return_value=''):
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                setup.main()
            self.assertIn('-100123', output.getvalue())
            self.assertNotIn('TEST_TOKEN', output.getvalue())
            launch.assert_not_called()

    def test_saved_start_reuses_only_matching_relay_without_opening_keyring(self):
        with tempfile.TemporaryDirectory() as project:
            profile = setup.fingerprint(os.path.normcase(str(Path(project).resolve())))
            relay_id = setup.fingerprint(profile + '/123')
            argv = ['setup', '--start-saved', '--project-dir', project, '--chat-id', '123']
            matching = dict(service='dreamy.debugger', fakeDelivery=False, localTelegramProfile=relay_id)
            with patch('sys.argv', argv), patch.object(setup, 'read_health', return_value=matching), \
                 patch.object(setup, 'saved_token') as token, patch.object(setup, 'start_relay') as start, \
                 contextlib.redirect_stdout(io.StringIO()):
                setup.main()
                token.assert_not_called()
                start.assert_not_called()

            with patch('sys.argv', argv), patch.object(setup, 'read_health', return_value=None), \
                 patch.object(setup, 'saved_token', return_value='123:MEMORY_ONLY') as token, \
                 patch.object(setup, 'start_relay') as start, patch.object(setup, 'bot_read') as upstream:
                setup.main()
                token.assert_called_once_with(profile, 'load')
                self.assertEqual(start.call_args.args[4:6], (relay_id, '8765'))
                upstream.assert_not_called()

    def test_conflicting_relay_uses_no_new_process(self):
        different = dict(service='dreamy.debugger', fakeDelivery=False, localTelegramProfile='elsewhere')
        with patch.object(setup, 'read_health', return_value=different), patch.object(setup.subprocess, 'Popen') as spawn:
            with self.assertRaisesRegex(SystemExit, 'port already in use'):
                setup.start_relay('123:TEST_TOKEN', '123', Path('.'), Path('.'), 'mine', '8765', reuse=True)
            spawn.assert_not_called()

    def test_credential_commands_and_chat_file_do_not_mix_token_and_settings(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(setup, 'credential_backend') as backend:
            store = backend.return_value
            store.get_password.return_value = '123:MEMORY_ONLY'
            setup.saved_token('profile', 'save', '123:MEMORY_ONLY')
            self.assertEqual(setup.saved_token('profile', 'load'), '123:MEMORY_ONLY')
            setup.saved_token('profile', 'forget')
            store.set_password.assert_called_once_with(setup.KEYRING_SERVICE, 'profile', '123:MEMORY_ONLY')
            store.delete_password.assert_called_once_with(setup.KEYRING_SERVICE, 'profile')
            data = Path(folder)
            setup.write_chat(data, '-100123')
            self.assertEqual((data / 'chat-id').read_text(), '-100123')
            self.assertEqual(list(data.iterdir()), [data / 'chat-id'])

    def test_discord_setup_hides_secret_and_persists_only_destination_hash(self):
        webhook = 'https://discord.com/api/webhooks/123/FIXTURE_SECRET'
        with tempfile.TemporaryDirectory() as folder:
            project = Path(folder)
            data = project / 'Library/DreamyDebugger/DiscordRelay'
            data.mkdir(parents=True)
            argv = ['setup', '--destination', 'discord', '--project-dir', folder, '--remember-token']
            with patch('sys.argv', argv), patch.object(setup, 'prepare_credential_python', return_value=Path(setup.sys.executable)), \
                 patch.object(setup, 'hidden_input', return_value=webhook) as hidden, \
                 patch.object(setup, 'verify_discord_webhook', return_value='1001'), \
                 patch.object(setup, 'saved_token') as saved, patch.object(setup, 'start_relay') as launch, \
                 contextlib.redirect_stdout(io.StringIO()) as output:
                setup.main()
            profile = setup.fingerprint(os.path.normcase(str(project.resolve())))
            relay_id = setup.fingerprint(profile + '/discord/1001/123')
            hidden.assert_called_once()
            saved.assert_called_once_with(profile, 'save', webhook, discord=True)
            self.assertEqual(launch.call_args.args[4:6], (relay_id, '8766'))
            self.assertEqual(launch.call_args.kwargs['destination'], 'discord')
            self.assertEqual((data / 'destination-id').read_text(), relay_id)
            self.assertNotIn(webhook, output.getvalue())

            saved_argv = ['setup', '--destination', 'discord', '--project-dir', folder, '--start-saved']
            telegram = dict(service='dreamy.debugger', fakeDelivery=False, localTelegramProfile=relay_id)
            with patch('sys.argv', saved_argv), patch.object(setup, 'read_health', return_value=telegram), \
                 patch.object(setup, 'saved_token', return_value=webhook), patch.object(setup, 'verify_discord_webhook') as verify:
                with self.assertRaisesRegex(SystemExit, 'port already in use'):
                    setup.main()
                verify.assert_not_called()

            discord = dict(service='dreamy.debugger', fakeDelivery=False, localDiscordProfile=relay_id)
            with patch('sys.argv', saved_argv), patch.object(setup, 'read_health', return_value=discord), \
                 patch.object(setup, 'saved_token') as saved, contextlib.redirect_stdout(io.StringIO()) as output:
                setup.main()
                saved.assert_not_called()
                self.assertIn('Discord relay already running', output.getvalue())

    def test_discord_validation_uses_read_only_request_and_hides_error_url(self):
        webhook = 'https://discord.com/api/webhooks/123/FIXTURE_SECRET'
        with self.assertRaises(ValueError):
            setup.normalize_webhook(webhook.replace('discord.com', 'example.test'))
        with patch.object(setup.urllib.request, 'build_opener') as open_request:
            open_request.return_value.open.return_value = io.BytesIO(b'{"type": 1, "channel_id": "1001"}')
            self.assertEqual(setup.verify_discord_webhook(webhook), '1001')
            req = open_request.return_value.open.call_args.args[0]
            self.assertEqual(req.get_method(), 'GET')
            self.assertEqual(req.get_header('User-agent'), setup.DISCORD_USER_AGENT)
            open_request.return_value.open.side_effect = urllib.error.HTTPError(webhook, 403, 'Forbidden', {}, None)
            with self.assertRaisesRegex(SystemExit, '(HTTP 403)') as error:
                setup.verify_discord_webhook(webhook)
            self.assertNotIn('FIXTURE_SECRET', str(error.exception))

    def test_bad_token_http_error_does_not_leak_url(self):
        error = urllib.error.HTTPError('https://api.telegram.org/botSECRET/getMe', 401, 'Unauthorized', {}, None)
        with patch.object(setup.urllib.request, 'urlopen', side_effect=error):
            with self.assertRaises(SystemExit) as result:
                setup.bot_read('SECRET', 'getMe')
            self.assertIn('401', str(result.exception))
            self.assertNotIn('SECRET', str(result.exception))
