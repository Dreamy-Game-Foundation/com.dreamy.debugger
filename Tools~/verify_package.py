"""Read-only release archive gate. Usage: python3 verify_package.py candidate.tgz"""
import hashlib
import json
from pathlib import Path, PurePosixPath
import sys
import tarfile


def verify(path):
    with tarfile.open(path) as archive:
        members = archive.getmembers()
        names = {entry.name for entry in members}
        for entry in members:
            parts = PurePosixPath(entry.name).parts
            if not parts or parts[0] != 'package' or '..' in parts or entry.issym() or entry.islnk():
                raise ValueError('Invalid archive member: ' + entry.name)
            if any(part in {'.git', '__pycache__', 'data', 'node_modules'} for part in parts):
                raise ValueError('Local state in archive: ' + entry.name)
            if entry.name.endswith(('.pyc', '.sqlite', '.sqlite-shm', '.sqlite-wal', '.db', '.log', '.pid', '.env')):
                raise ValueError('Local state in archive: ' + entry.name)
        for entry in members:
            if entry.isfile() and not entry.name.endswith('.meta') and not any(part.endswith('~') for part in PurePosixPath(entry.name).parts):
                if entry.name + '.meta' not in names:
                    raise ValueError('Missing Unity metadata: ' + entry.name)
        required = ['package.json', 'README.md', 'CHANGELOG.md',
                    'Documentation~/RELEASE.md', 'Documentation~/Graphy-MIT-LICENSE.txt',
                    'Samples~/QuickStart/link.xml', 'Tools~/relay/server.py']
        for name in required:
            if 'package/' + name not in names:
                raise ValueError('Missing release file: ' + name)
        manifest = json.load(archive.extractfile('package/package.json'))
        if manifest['name'] != 'com.dreamy.debugger':
            raise ValueError('Wrong package identity')
    return dict(name=manifest['name'], version=manifest['version'], entries=len(members),
                sha256=hashlib.sha256(Path(path).read_bytes()).hexdigest(),
                bytes=Path(path).stat().st_size, result='passed')


if __name__ == '__main__':
    print(json.dumps(verify(sys.argv[1]), indent=2))
