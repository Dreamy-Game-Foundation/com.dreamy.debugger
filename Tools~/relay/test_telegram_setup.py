import contextlib
import io
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import urllib.error
import telegram_setup as setup

class SetupTests(unittest.TestCase):
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

    def test_bad_token_http_error_does_not_leak_url(self):
        error = urllib.error.HTTPError('https://api.telegram.org/botSECRET/getMe', 401, 'Unauthorized', {}, None)
        with patch.object(setup.urllib.request, 'urlopen', side_effect=error):
            with self.assertRaises(SystemExit) as result:
                setup.bot_read('SECRET', 'getMe')
            self.assertIn('401', str(result.exception))
            self.assertNotIn('SECRET', str(result.exception))
