"""Verify a UPM Git installation against its exact committed source revision.

Usage: python3 verify_git_install.py <source-repository> <resolved-package-directory>
Checkout the resolved commit in source-repository first. No files are modified.
"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def verify(source, installed):
    source, installed = Path(source).resolve(), Path(installed).resolve()
    files = subprocess.check_output(
        ['git', '-C', str(source), 'ls-files', '-z'], text=True).split('\0')
    commit = subprocess.check_output(
        ['git', '-C', str(source), 'rev-parse', 'HEAD'], text=True).strip()
    missing, different, checked = [], [], 0
    # UPM removes repository ignore configuration from resolved packages.
    for name in files:
        if not name or name == '.gitignore':
            continue
        expected, actual = source / name, installed / name
        if not expected.is_file():
            continue
        checked += 1
        if not actual.is_file():
            missing.append(name)
        elif name == 'package.json':
            expected_manifest = json.loads(expected.read_text())
            actual_manifest = json.loads(actual.read_text())
            # UPM adds the resolved Git hash; all authored fields must still match.
            fingerprint = actual_manifest.pop('_fingerprint', None)
            if expected_manifest != actual_manifest or fingerprint not in (None, commit):
                different.append(name)
        elif hashlib.sha256(expected.read_bytes()).digest() != hashlib.sha256(actual.read_bytes()).digest():
            different.append(name)
    return dict(result='passed' if not missing and not different else 'failed',
                sourceCommit=commit,
                checked=checked, missing=missing, different=different)


if __name__ == '__main__':
    result = verify(sys.argv[1], sys.argv[2])
    print(json.dumps(result, indent=2))
    sys.exit(0 if result['result'] == 'passed' else 1)
