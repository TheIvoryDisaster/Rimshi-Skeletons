"""Owner-run SteamCMD adapter. Reuses a locally authorized session; no password collection."""
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

ITEM = '3841311958'


def command(executable, username, vdf):
    if not re.fullmatch(r'[A-Za-z0-9_]{2,64}', username):
        raise ValueError('Invalid Steam login account name')
    if not executable.is_absolute() or not executable.is_file() or executable.name.lower() != 'steamcmd.exe':
        raise ValueError('STEAMCMD_PATH must name the installed steamcmd.exe')
    return [str(executable), '+login', username, '+workshop_build_item', str(vdf), '+quit']


def main():
    # No untrusted event can authorize a persistent credential-bearing runner.
    if (os.environ.get('GITHUB_REPOSITORY') != 'TheIvoryDisaster/Rimshi-Skeletons'
            or os.environ.get('GITHUB_ACTOR_ID') != '17211998'
            or os.environ.get('GITHUB_TRIGGERING_ACTOR') != 'TheIvoryDisaster'
            or os.environ.get('GITHUB_EVENT_NAME') != 'workflow_dispatch'
            or os.environ.get('GITHUB_REF') != 'refs/heads/main'
            or os.environ.get('CONFIRM_ITEM') != ITEM):
        raise ValueError('Owner-only manual main-branch publishing required')
    output = Path(os.environ['WORKSHOP_STAGE']).resolve()
    plan = json.loads((output / 'plan.json').read_text())
    if str(plan.get('workshop_item_id', plan.get('item_id'))) != ITEM:
        raise ValueError('Unexpected Workshop item in validated plan')
    vdf = output / 'workshop-preview.vdf'
    # The central validator generates this file. Windows paths need VDF-safe slashes.
    vdf.write_text(vdf.read_text().replace('\\', '/'), encoding='utf-8')
    executable = Path(os.environ['STEAMCMD_PATH'])
    args = command(executable, os.environ['STEAM_USERNAME'], vdf)
    # Raw Steam output may contain account/session information. Never publish or cache it.
    with tempfile.TemporaryFile(mode='w+b') as log:
        process = subprocess.Popen(args, cwd=executable.parent, stdin=subprocess.DEVNULL,
                                   stdout=log, stderr=subprocess.STDOUT)
        try:
            result = process.wait(timeout=600)
        except subprocess.TimeoutExpired:
            subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True)
            raise RuntimeError('SteamCMD timed out; reauthorize Steam Guard locally and retry') from None
        log.seek(0)
        text = log.read(8 * 1024 * 1024).decode('utf-8', errors='replace')
    if result != 0 or not re.search(r'Success\.\s*Published item\s+' + ITEM + r'\b', text, re.I):
        raise RuntimeError('SteamCMD did not confirm the expected item update. Check login locally; raw output was withheld.')
    print('SteamCMD confirmed update of Workshop item ' + ITEM)


if __name__ == '__main__':
    try:
        main()
    except Exception:
        raise SystemExit('Workshop upload failed or needs local Steam authorization. No raw Steam output is exposed.')
