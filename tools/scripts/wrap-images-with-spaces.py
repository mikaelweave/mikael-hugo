import os
import re

def wrap_image_urls(directory):
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith(".md"):
                filepath = os.path.join(root, file)
                with open(filepath, 'r+') as f:
                    content = f.read()
                    # Regex pattern to find markdown image syntax with spaces in URL
                    pattern = r'!\[\]\(([^)]* [^)]*)\)'
                    # Replace with angle brackets around URL
                    content_new = re.sub(pattern, r'![](<\1>)', content)
                    if content != content_new:
                        f.seek(0)
                        f.write(content_new)
                        f.truncate()

# Get the directory of the script file
script_dir = os.path.dirname(os.path.realpath(__file__))
# Get the directory two levels above the script file
two_dirs_up = os.path.abspath(os.path.join(script_dir, '..', '..'))

wrap_image_urls(two_dirs_up)