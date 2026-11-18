"""Release validator from GutenStructure 00fa7847, with Windows VDF path normalization. No Steam operations."""
import argparse
import hashlib
import json
import os
import re
import shutil
import stat
import struct
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path, PurePosixPath

MAX_ZIP = 96 * 1024 * 1024
MAX_EXPANDED = 512 * 1024 * 1024


def stage(archive, output, expected_sha256, package_id, item_id):
    if not re.fullmatch(r'[a-f0-9]{64}', expected_sha256):
        raise ValueError('Exact lowercase SHA-256 required')
    if not re.fullmatch(r'[A-Za-z0-9_.-]{1,120}', package_id) or not re.fullmatch(r'[1-9][0-9]{0,19}', item_id):
        raise ValueError('Valid package ID and existing Workshop item required')
    archive, output = Path(archive), Path(output).resolve()
    if not 0 < archive.stat().st_size <= MAX_ZIP or hashlib.sha256(archive.read_bytes()).hexdigest() != expected_sha256:
        raise ValueError('ZIP size or SHA-256 mismatch')
    with zipfile.ZipFile(archive) as source:
        entries, seen, roots, expanded = [], set(), set(), 0
        for info in source.infolist():
            name = info.filename
            raw = name.rstrip('/').split('/')
            if not name or name.startswith('/') or any(c in name for c in '\\:\x00\n\r\t') or any(p in ('', '.', '..') for p in raw):
                raise ValueError('Unsafe ZIP path')
            mode = info.external_attr >> 16
            if info.flag_bits & 1 or stat.S_ISLNK(mode) or stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR):
                raise ValueError('Encrypted or special ZIP entry')
            normalized = '/'.join(raw).casefold()
            if normalized in seen:
                raise ValueError('Duplicate ZIP path')
            seen.add(normalized)
            roots.add(raw[0])
            expanded += info.file_size
            entries.append(info)
            if expanded > MAX_EXPANDED or len(entries) > 10000:
                raise ValueError('Expanded ZIP exceeds limits')
            leaf = raw[-1].lower()
            if leaf in ('assembly-csharp.dll','0harmony.dll','mscorlib.dll','netstandard.dll') or leaf.startswith(('unityengine','system.')) and leaf.endswith('.dll'):
                raise ValueError('Game/dependency assemblies must not be redistributed in the mod')
            if any(part.casefold() in ('.git', '.github', 'source') for part in raw):
                raise ValueError('Source/control metadata must not enter the Workshop package')
        if len(roots) != 1:
            raise ValueError('Expected one top-level mod folder')
        root = next(iter(roots))
        if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,120}', root):
            raise ValueError('Mod root is not safe for VDF staging')
        def bounded(relative, limit):
            info = source.getinfo(root + '/' + relative)
            if info.file_size > limit:
                raise ValueError('Metadata exceeds limits')
            return source.read(info)
        about = ET.fromstring(bounded('About/About.xml', 256 * 1024))
        if about.tag != 'ModMetaData' or about.findtext('packageId') != package_id:
            raise ValueError('RimWorld package identity mismatch')
        if bounded('About/PublishedFileId.txt', 64).decode('utf-8-sig').strip() != item_id:
            raise ValueError('PublishedFileId does not match the existing Workshop item')
        preview = bounded('About/Preview.png', 8 * 1024 * 1024)
        if len(preview) < 24 or preview[:8] != b'\x89PNG\r\n\x1a\n' or preview[12:16] != b'IHDR':
            raise ValueError('Workshop Preview.png is not a PNG')
        width, height = struct.unpack('>II', preview[16:24])
        if not (0 < width <= 8192 and 0 < height <= 8192):
            raise ValueError('Preview dimensions exceed limits')
        versions = [entry.text for entry in about.findall('supportedVersions/li')]
        if not versions or any(not re.fullmatch(r'1\.[0-9]+', value or '') for value in versions):
            raise ValueError('Supported game versions must be explicit')
        names = {entry.filename for entry in entries if not entry.is_dir()}
        for version in versions:
            if not any(name.startswith(f'{root}/{version}/Assemblies/') and name.endswith('.dll') for name in names):
                raise ValueError('An advertised game version has no mod assembly')
        if source.testzip() is not None:
            raise ValueError('ZIP CRC check failed')
        if '"' in output.as_posix() or '\n' in output.as_posix() or '\r' in output.as_posix():
            raise ValueError('Unsafe staging path for VDF')
        output.mkdir(parents=True, mode=0o700, exist_ok=False)
        for info in entries:
            target = output.joinpath(*PurePosixPath(info.filename).parts)
            if info.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with source.open(info) as incoming, target.open('xb') as destination:
                    shutil.copyfileobj(incoming, destination)
                target.chmod(0o600)
    receipt = {'mode':'preview', 'uploaded':False, 'steam_app_id':294100, 'workshop_item_id':item_id,
               'package_id':package_id, 'sha256':expected_sha256, 'mod_version':about.findtext('modVersion'),
               'supported_versions':versions, 'files':len(names), 'expanded_bytes':expanded,
               'preview_dimensions':[width,height], 'visibility':'not read or changed; verify before any future live update'}
    (output / 'plan.json').write_text(json.dumps(receipt, indent=2) + '\n')
    # Omit visibility/title/description/previewfile: a future adapter must not replace listing metadata by default.
    (output / 'workshop-preview.vdf').write_text('"workshopitem"\n{\n  "appid" "294100"\n'
        f'  "publishedfileid" "{item_id}"\n  "contentfolder" "{(output / root).as_posix()}"\n}}\n')
    return receipt


def gh_api(path):
    result = subprocess.run(['gh','api',path], check=True, capture_output=True, text=True)
    return json.loads(result.stdout)


def fetch_release(tag, asset_id, digest, target):
    repository = os.environ['GITHUB_REPOSITORY']
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository) or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._+-]{0,127}', tag):
        raise ValueError('Invalid caller repository or tag')
    if not re.fullmatch(r'[1-9][0-9]{0,19}', asset_id) or not re.fullmatch(r'[a-f0-9]{64}', digest):
        raise ValueError('Exact asset ID and digest required')
    metadata = gh_api(f'repos/{repository}')
    if metadata['owner']['type'] != 'User' or str(metadata['owner']['id']) != os.environ['GITHUB_REPOSITORY_OWNER_ID'] or str(metadata['id']) != os.environ['GITHUB_REPOSITORY_ID']:
        raise ValueError('Source must be the personally owned caller repository')
    release = gh_api(f'repos/{repository}/releases/tags/{tag}')
    if release.get('draft') or release.get('prerelease') or release['tag_name'] != tag:
        raise ValueError('Published stable release required')
    ref = gh_api(f'repos/{repository}/git/ref/tags/{tag}')['object']
    for _ in range(4):
        if ref['type'] == 'commit':
            break
        if ref['type'] != 'tag' or not re.fullmatch(r'[a-f0-9]{40}', ref['sha']):
            raise ValueError('Invalid release tag')
        ref = gh_api(f'repos/{repository}/git/tags/{ref["sha"]}')['object']
    if ref['type'] != 'commit' or not re.fullmatch(r'[a-f0-9]{40}', ref['sha']):
        raise ValueError('Release tag must resolve to a commit')
    comparison = gh_api(f'repos/{repository}/compare/{ref["sha"]}...{metadata["default_branch"]}')
    if comparison['status'] not in ('ahead','identical'):
        raise ValueError('Release commit must be in default-branch history')
    asset = gh_api(f'repos/{repository}/releases/assets/{asset_id}')
    member = False
    for page in range(1, 11):
        assets = gh_api(f'repos/{repository}/releases/{release["id"]}/assets?per_page=100&page={page}')
        if any(str(value['id']) == asset_id for value in assets):
            member = True
            break
        if len(assets) < 100:
            break
    if not member or asset.get('digest') != 'sha256:' + digest or asset.get('state') != 'uploaded' or not 0 < asset.get('size',0) <= MAX_ZIP:
        raise ValueError('Release asset membership, digest, state or size mismatch')
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,150}\.zip', asset['name']):
        raise ValueError('ZIP asset name is invalid')
    subprocess.run(['gh','release','download',tag,'--repo',repository,'--pattern',asset['name'],'--output',str(target)], check=True, capture_output=True)
    if target.stat().st_size != asset['size'] or hashlib.sha256(target.read_bytes()).hexdigest() != digest:
        raise ValueError('Downloaded release bytes do not match approved asset')
    return ref['sha']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path)
    parser.add_argument('--output', type=Path, default=Path('workshop-preview'))
    parser.add_argument('--sha256', required=True)
    parser.add_argument('--package-id', required=True)
    parser.add_argument('--item-id', required=True)
    parser.add_argument('--release-tag')
    parser.add_argument('--asset-id')
    args = parser.parse_args()
    try:
        with tempfile.TemporaryDirectory() as temporary:
            archive = args.archive or Path(temporary) / 'release.zip'
            commit = None
            if not args.archive:
                if not args.release_tag or not args.asset_id:
                    raise ValueError('Supply local archive or release tag and asset ID')
                commit = fetch_release(args.release_tag, args.asset_id, args.sha256, archive)
            plan = stage(archive, args.output, args.sha256, args.package_id, args.item_id)
            if commit:
                plan.update(source_sha=commit, source_repository=os.environ['GITHUB_REPOSITORY'], release_tag=args.release_tag, asset_id=args.asset_id)
                (args.output/'plan.json').write_text(json.dumps(plan, indent=2)+'\n')
            print(json.dumps(plan, indent=2))
    except (ValueError, KeyError, OSError, zipfile.BadZipFile, ET.ParseError, subprocess.SubprocessError):
        parser.exit(2, 'Workshop preview failed validation or release retrieval; no Steam operation was attempted.\n')


if __name__ == '__main__':
    main()
