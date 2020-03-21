from azure.storage.blob import BlockBlobService, PublicAccess, baseblobservice
import os, pathlib, re, asyncio, sys
from git import Repo

def run(STORAGE_ACCOUNT_NAME, STORAGE_ACCOUNT_KEY, push_everything):
    """
    Pushes images from generated Hugo output public folder to Azure
    """
    # Script helper validators - let's not do bad things
    if STORAGE_ACCOUNT_NAME == "" or STORAGE_ACCOUNT_KEY == "":
        raise Exception("Must have STORAGE_ACCOUNT_NAME and STORAGE_ACCOUNT_KEY env variables set!")

    # Returns a list of changes in the repo
    def find_git_changes():
        repo = Repo(os.getcwd())
        return repo.untracked_files + [o.b_path for o in repo.index.diff(None)]

    async def push_imgs_in_dir(directory, blob_container_name, prefix = ''):
        print(f"Processing {directory}...")
        block_blob_service = BlockBlobService(account_name=STORAGE_ACCOUNT_NAME, account_key=STORAGE_ACCOUNT_KEY)
        block_blob_service.create_container(blob_container_name)
        block_blob_service.set_container_acl(blob_container_name, public_access=PublicAccess.Container)

        # Load srcset 
        #with open(srcset_file_path) as f:
        #    srcsets = json.load(f)
        #QUICK_PROCESS = True

        blob_reference = {}
        # It's helpful to just have a list of blobs in the container
        for blob in block_blob_service.list_blobs(blob_container_name, prefix=prefix):
            # skip already processed or not supported blobs
            supported_image = re.compile(r'.*\.(jpg|jpeg|gif|png)$')
            already_processed = re.compile(r'.*_[0-9]+w\.[a-zA-Z]+$')
            if not already_processed.match(blob.name) and supported_image.match(blob.name):
                blob_reference[blob.name] = blob.properties.content_length

        last_dir = ""
        for subdir, dirs, files in os.walk(directory):
            if last_dir != subdir:
                last_dir = subdir
                print(f'Subdirectory {last_dir}...') 

            for file in files:
                if pathlib.Path(file).suffix.lower() in ['.jpg', '.jpeg', '.png', 'gif']:
                    file_path = os.path.realpath(os.path.join(subdir, file))
                    blob_name = file_path.replace(f'{os.path.realpath(directory)}/', "")

                    # Skip unchanged files - probably could use a better approach here
                    if blob_name in blob_reference and blob_reference[blob_name] == os.path.getsize(file_path):
                        continue

                    # Write blob and associated metadata
                    print(f'Pushing image {file_path}...')
                    block_blob_service.create_blob_from_path(container_name=blob_container_name, blob_name=blob_name, file_path=file_path)

    try:
        push_paths = filter(lambda x: len(x) > 0, [os.path.dirname(x) for x in find_git_changes()])

        if push_everything:
            # Push static images
            asyncio.run(push_imgs_in_dir("static/img/", "img"))
            # Iterate through content dirs, if they have a single image, process whole dir
            for directory in next(os.walk('content/'))[1]:
                asyncio.run(push_imgs_in_dir(os.path.join("content/", directory), directory))
        else:
            # Push static images if changes detected
            if any(path.startswith('layouts') or path == 'static' for path in push_paths):
                asyncio.run(push_imgs_in_dir("static/img/", "img"))
            # Push content images
            for path in filter(lambda p: p.startswith('content') and p != "content/_index.md", push_paths):
                asyncio.run(push_imgs_in_dir(path, path.split('/')[1], '/'.join(path.split('/')[1:-1])))

    except Exception as err:
        print(f"Error pushing to Azure. {err}") 

    print("Done pushing images!")
    return

from dotenv import load_dotenv, find_dotenv
load_dotenv(find_dotenv())

BLOB_ACCOUNT_NAME = os.getenv("BLOB_ACCOUNT_NAME")
BLOB_ACCOUNT_KEY = os.getenv("BLOB_ACCOUNT_KEY")

# if true is passed, push everything
if len(sys.argv) > 1 and sys.argv[1] == 'True':
    run(BLOB_ACCOUNT_NAME, BLOB_ACCOUNT_KEY, True)
else:
    run(BLOB_ACCOUNT_NAME, BLOB_ACCOUNT_KEY, False)