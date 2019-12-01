import pathlib, os, re, platform, sys
from subprocess import Popen, PIPE, call
from pathlib import Path


def check_execs_posix_win(progs):
    """Check if the program is installed.
    Returns one  dictionary with 1+n pair of key/values:
    A fixed key/value:
    "WinOS" -- (boolean) True it's a Windows OS, False it's a *nix OS
    for each program in progs a key/value like this:
    "program"  -- (str or boolean) The Windows executable path if founded else
                                   '' if it's Windows OS. If it's a *NIX OS
                                   True if founded else False
    """
    execs = {'WinOS':  True if platform.system() == 'Windows' else False}
    # get all the drive unit letters if the OS is Windows
    windows_drives = re.findall(r'(\w:)\\',
                                Popen('fsutil fsinfo drives', stdout=PIPE).
                                communicate()[0]) if execs['WinOS'] else None

    progs = [progs] if isinstance(progs, str) else progs
    for prog in progs:
        if execs['WinOS']:
            # Set all commands to search the executable in all drives
            win_cmds = ['dir /B /S {0}\*{1}.exe'.format(letter, prog) for
                        letter in windows_drives]
            # Get the first location (usually C:) where the executable exists
            for cmd in win_cmds:
                execs[prog] = (Popen(cmd, stdout=PIPE, stderr=PIPE, shell=1).
                               communicate()[0].split(os.linesep)[0])
                if execs[prog]:
                    break
        else:
            try:
                Popen([prog, '--help'], stdout=PIPE, stderr=PIPE)
                execs[prog] = True
            except OSError:
                execs[prog] = False
    return execs

# Get tools
EXECS = check_execs_posix_win(['jpegtran', 'pngcrush', 'gifsicle'])
jpegtran = EXECS['jpegtran'] if EXECS['WinOS'] else 'jpegtran'
pngcrush = EXECS['pngcrush'] if EXECS['WinOS'] else 'pngcrush'
gifsicle = EXECS['gifsicle'] if EXECS['WinOS'] else 'gifsicle'


def run():
    def optimze_imgs_in_dir(directory):
        # Get the executable's names (and path for windows) of the needed programs
        for subdir, dirs, files in os.walk(directory):
            for file in files:
                file_path = pathlib.Path(f'{subdir}/{file}')
                outfile = pathlib.Path(f'resize_output/{subdir}/{file}')
                if not os.path.isdir(f'resize_output/{subdir}'):
                    Path(f'resize_output/{subdir}').mkdir(parents=True)

                if file_path.suffix.lower() in ['.jpg', '.jpeg'] and not os.path.exists(outfile):
                    call([jpegtran, '-copy', 'all', '-optimize', '-perfect', '-outfile', file_path, outfile])
                elif file_path.suffix.lower() == '.png' and not os.path.exists(outfile):
                    call([pngcrush, '-rem', 'alla', '-reduce', '-brute', file_path, outfile])
                elif file_path.suffix.lower() == '.gif' and not os.path.exists(outfile):
                    call([gifsicle, '-O2', file_path, "--output", outfile])

    # Push static images
    optimze_imgs_in_dir("static/img/")
    # Iterate through content dirs, if they have a single image, process whole dir
    for directory in next(os.walk('content/'))[1]:
        for subdir, dirs, files in os.walk(os.path.join("content/", directory)):
            for file in files:
                if pathlib.Path(file).suffix.lower() in ['.jpg', '.jpeg', '.png', 'gif']:
                    optimze_imgs_in_dir(os.path.join("content/", directory))
                    break

run()