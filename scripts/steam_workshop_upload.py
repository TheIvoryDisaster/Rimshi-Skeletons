"""Owner-run SteamCMD adapter on an ephemeral GitHub-hosted Windows runner."""
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


def run_script(username, password, guard, vdf_path, mode):
    if mode not in ('login-test', 'upload'):
        raise ValueError('Unknown Steam mode')
    return ('@ShutdownOnFailedCommand 1\n@NoPromptForPassword 1\n'
        + f'login "{username}" "{password}"' + (f' "{guard}"' if guard else '')
        + (f'\nworkshop_build_item "{vdf_path}"' if mode == 'upload' else '')
        + '\nquit\n')


def main():
    # No untrusted event can authorize a persistent credential-bearing runner.
    if (os.environ.get('GITHUB_REPOSITORY') != 'TheIvoryDisaster/Rimshi-Skeletons'
            or os.environ.get('GITHUB_ACTOR_ID') != '17211998'
            or os.environ.get('GITHUB_TRIGGERING_ACTOR') != 'TheIvoryDisaster'
            or os.environ.get('GITHUB_EVENT_NAME') != 'workflow_dispatch'
            or os.environ.get('GITHUB_REF') != 'refs/heads/main'
            or os.environ.get('CONFIRM_ITEM') != ITEM):
        raise ValueError('Owner-only manual main-branch publishing required')
    mode = os.environ.get('STEAM_MODE', 'login-test')
    if mode not in ('login-test', 'upload'):
        raise ValueError('Unknown Steam mode')
    output = Path(os.environ['WORKSHOP_STAGE']).resolve()
    plan = json.loads((output / 'plan.json').read_text())
    if str(plan.get('workshop_item_id', plan.get('item_id'))) != ITEM:
        raise ValueError('Unexpected Workshop item in validated plan')
    vdf = output / 'workshop-preview.vdf'
    # The central validator generates this file. Windows paths need VDF-safe slashes.
    vdf.write_text(vdf.read_text().replace('\\', '/'), encoding='utf-8')
    executable = Path(os.environ['STEAMCMD_PATH'])
    username = os.environ['STEAM_USERNAME']
    command(executable, username, vdf)  # Validate account/path without exposing credentials.
    password = os.environ['STEAM_PASSWORD']
    guard = os.environ.get('STEAM_GUARD_CODE', '')
    if not password or any(c in password for c in ('"', chr(92), '\r', '\n')) or (guard and not re.fullmatch(r'[A-Za-z0-9]{5,8}', guard)):
        raise ValueError('Missing password or unsupported credential characters')
    # Run-script keeps secrets out of command-line arguments and GitHub output.
    # Both this file and raw output are ephemeral, never uploaded or cached.
    with tempfile.TemporaryDirectory() as temporary:
        script = Path(temporary) / 'upload.txt'
        vdf_path = str(vdf).replace(chr(92), '/')
        script.write_text(run_script(username, password, guard, vdf_path, mode), encoding='utf-8')
        with tempfile.TemporaryFile(mode='w+b') as log:
            child_env = {k:v for k,v in os.environ.items() if k not in ('STEAM_PASSWORD', 'STEAM_GUARD_CODE', 'GH_TOKEN', 'GITHUB_TOKEN')}
            process = subprocess.Popen([str(executable), '+runscript', str(script)], cwd=executable.parent,
                                       env=child_env, stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT)
            try:
                result = process.wait(timeout=600)
            except subprocess.TimeoutExpired:
                subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True)
                raise RuntimeError('SteamCMD timed out; refresh Steam Guard authorization and retry') from None
            log.seek(0)
            text = log.read(8 * 1024 * 1024).decode('utf-8', errors='replace')
    if mode == 'login-test':
        if result == 0 and re.search(r'Waiting for user info.*OK', text, re.I):
            print('Steam login confirmed; no Workshop upload attempted.')
            return
        if re.search(r'Steam Guard|two.factor|authenticator|AccountLogonDenied|code mismatch', text, re.I):
            print('Steam requires additional Guard authorization; no Workshop upload attempted.')
        elif re.search(r'InvalidPassword|Invalid Password', text, re.I):
            print('Steam rejected the credentials; no Workshop upload attempted.')
        else:
            print('Steam login was not confirmed; raw output withheld; no upload attempted.')
        raise RuntimeError('Login test needs owner attention')
    if result != 0 or not re.search(r'Success\.\s*Published item\s+' + ITEM + r'\b', text, re.I):
        raise RuntimeError('SteamCMD did not confirm the expected item update. Check login locally; raw output was withheld.')
    print('SteamCMD confirmed update of Workshop item ' + ITEM)


if __name__ == '__main__':
    try:
        main()
    except Exception:
        raise SystemExit('Workshop upload failed or needs local Steam authorization. No raw Steam output is exposed.')
