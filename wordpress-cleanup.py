############################################################
# Script Params

# Path of the markdown exported by Hugo Wordpress Exporter
export_path = "C:\\Users\\Mikael\\Downloads\\hugo-export\\"
#export_path = "/mnt/c/Users/Mikael/Downloads/hugo-export/"

# Path to put the Hugo files
new_path = "C:\\source\\mikaelstadden-hugo\\content\\photography\\"
#new_path = "/mnt/c/source/mikaelstadden-hugo/content/photography/"

# Site URL - used to make img links relevant to the current domain
site_url = "mikaelstadden.com"

#############################################################

import os
import re
from shutil import copyfile

# Copy and cleanup files
for filename in os.listdir(os.path.abspath(export_path + "/posts/")):
    # Extract out post info
    year = filename.split("-")[0]
    month = filename.split("-")[1]
    day = filename.split("-")[2]
    title = "-".join(filename.split("-")[3:]).replace(".md", "")

    # Make the directory
    new_folder_path=os.path.abspath(new_path + "/" + year + "/" + title + "/")
    os.makedirs(new_folder_path, exist_ok=True)

    # Open pointer to new file path
    new_file_path = os.path.abspath(new_folder_path +  '/index.md')
    if os.path.exists(new_file_path):
        os.remove(new_file_path)
    
    # Open old file
    with open(os.path.abspath(export_path + '/posts/' + filename), encoding="utf8") as f:
        file_contents = f.read()

    # Cleanup image links - we want them to be relevant to host so we can work locally
    file_contents = file_contents.replace('http://' + site_url, '')
    file_contents = file_contents.replace('http://www.' + site_url, '')
    file_contents = file_contents.replace('https://' + site_url, '')
    file_contents = file_contents.replace('https://www.' + site_url, '')

    # Remove DIVs - they are irrelevant here
    pattern = re.compile(r"<div.*?>")
    file_contents = re.sub(pattern, "", file_contents)
    file_contents = file_contents.replace("</div>", "")

    # Copy images (only standard size) to same folder of md file
    pattern = re.compile(r'<img class=".*?" src=".*?"')
    img_refs = re.findall(pattern, file_contents)
    for match in img_refs:
        pattern = re.compile(r'src=".*?"')
        src_ref = re.findall(pattern, match)[0].replace('src="', "")[:-1]
        # Copy image
        src_ref = src_ref.replace("/photography/", "")
        copyfile(os.path.abspath(export_path + "/" + src_ref), os.path.abspath(new_folder_path + "/" + src_ref.replace("\\", "/").split('/')[-1]))

    # Fix image refs
    def firstreffileonly(matchobj):
        return '{{< imgproc "' + matchobj.group(1).replace("\\", "/").split('/')[-1] + '" "' + matchobj.group(2) + '" >}}'
    pattern = re.compile(r'<img class=".*" src="(.*)" alt="(.*)" width="([0-9]*)" height="([0-9]*)" srcset="(.*)" sizes="(.*)" />')
    file_contents = re.sub(pattern, firstreffileonly, file_contents)

    # Remove styles
    pattern = re.compile(r' style=".*?"')
    file_contents = re.sub(pattern, "", file_contents)

    # Fix links
    pattern = re.compile(r'<a href="(.*?)" target="_blank" rel="noopener">(.*?)</a>')
    file_contents = re.sub(pattern, r"[\2](\1){:target=\"_blank\"}", file_contents)
    pattern = re.compile(r'<a href="(.*?)">(.*?)</a>')
    file_contents = re.sub(pattern, r"[\2](\1)", file_contents)

    # Remove span
    file_contents = file_contents.replace("<span>", "")
    file_contents = file_contents.replace("<span >", "")
    file_contents = file_contents.replace("</span>", "")

    # Remove p
    file_contents = file_contents.replace("<p>", "")
    file_contents = file_contents.replace("</p>", "")

    # General cleanup
    file_contents = file_contents.replace('&#8217;', "'") # apostrophe
    file_contents = file_contents.replace('&#8211;', "–") # en dash
    file_contents = file_contents.replace('&nbsp;', "") # non-breaking space
    file_contents = file_contents.replace('&#8230;', "...") # ELLIPSIS
    pattern = re.compile(r"<b>(.*)</b>") # bold
    file_contents = re.sub(pattern, r"**\1**", file_contents)
    pattern = re.compile(r"<strong>(.*?)</strong>") # bold
    file_contents = re.sub(pattern, r"**\1**", file_contents)
    file_contents = file_contents.replace('<br />', '\n') # line break
    pattern = re.compile(r"\n +([A-Za-z{\*\[])") # lines shouldn't start with space
    file_contents = re.sub(pattern, r"\1", file_contents)
    pattern = re.compile(r"\n\n[\n]+") # normalize newline
    file_contents = re.sub(pattern, "\n\n", file_contents)

    # Write to new location
    with open(new_file_path, 'w', encoding="utf8") as f:
        f.write(file_contents)