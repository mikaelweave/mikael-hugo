import os
import re

def update_markdown_file(file_path):
    with open(file_path, 'r') as file:
        content = file.read()

    pattern = r'!\[([^\]]+)\]\(([^)]+)\)'
    def replacement(match):
        if ' ' in match.group(2) and not match.group(2).startswith('<') and not match.group(2).endswith('>'):
            print(f'Updating image in file: {file_path} - {match.group(2)}')
            return f'![{match.group(1)}](<{match.group(2)}>)'
        return match.group(0)

    modified_content = re.sub(pattern, replacement, content)

    with open(file_path, 'w') as file:
        file.write(modified_content)

def update_markdown_files_in_directory(directory):
    for root, _, files in os.walk(directory):
        for file in files:
            if file.endswith('.md'):
                file_path = os.path.join(root, file)
                update_markdown_file(file_path)

# Replace 'your_directory_path' with the path to your directory
update_markdown_files_in_directory('./content/')