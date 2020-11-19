import os, requests, glob, yaml
from pathlib import Path

# Given the path to a Hugo content file, will return the front matter and markdown
def load_content_file(file_path):
    try:
        from yaml import CLoader as Loader, CDumper as Dumper
    except ImportError:
        from yaml import Loader, Dumper


    with open(file_path, 'r+') as content_file:
        content = content_file.read()

        front_matter, *markdown = list(filter(None, content.split("---")))

        # TODO - clean this up, why is this being cast into an array??
        if markdown == []:
            markdown = ""
        else:
            markdown = markdown[0]

        yaml_front_matter = yaml.load(front_matter, Loader=Loader)

        return yaml_front_matter, markdown


# Given a content file path, url, and relative path, will download file 
def download_dependency(content_file, relative_download_path, url):

    # Setup dir helpers
    content_file_dir = os.path.dirname(os.path.abspath(content_file))
    abs_path = content_file_dir + '/' + relative_download_path
    abs_path_dir = Path('/'.join(abs_path.split('/')[0:-1]))

    # Create children folders if needed
    abs_path_dir.mkdir(parents=True, exist_ok=True)

    # Download file
    r = requests.get(url)
    with open(abs_path, 'wb') as f:
        f.write(r.content)


# Load all content files
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
content_files = glob.glob(SCRIPT_DIR + '/../content/**/*.md', recursive=True)

for file in content_files:
    front_matter, markdown = load_content_file(file)

    if 'download-files' in front_matter:
        for download_path, url in front_matter['download-files'].items():
            download_dependency(file, download_path, url)