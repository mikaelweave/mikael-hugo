#!/usr/bin/env python3

import os, time, random
from azure.storage.blob import BlockBlobService, PublicAccess, baseblobservice
from azure.common import AzureConflictHttpError
from concurrent.futures import ThreadPoolExecutor

from dotenv import load_dotenv, find_dotenv
load_dotenv(find_dotenv())

BLOB_ACCOUNT_NAME = os.getenv("CDN_BLOB_ACCOUNT_NAME")
BLOB_ACCOUNT_KEY = os.getenv("CDN_BLOB_ACCOUNT_KEY")
DOP = 16

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
block_blob_service = BlockBlobService(BLOB_ACCOUNT_NAME, BLOB_ACCOUNT_KEY)

blobs_to_pull = []

containers = block_blob_service.list_containers()
for container in containers:
    if container.name.startswith("azure") or container.name.startswith("$"):
        continue
    for blob in block_blob_service.list_blobs(container_name=container.name):
        if not blob.name.endswith("srcsets.json"):
            continue
        if container == "img":
            file_path = f'/../data/img/{blob.name}'
        else:
            file_path = f'/../data/{container.name}/{blob.name}'

        file_path = os.path.dirname(os.path.abspath(__file__)) + file_path
        
        blobs_to_pull.append((container.name, blob.name, file_path))

def pull_srcset_file(el):
    print(f'Pulling to {el[2]}...')

    if not os.path.exists(os.path.dirname(el[2])):
            os.makedirs(os.path.dirname(el[2]))

    lease_id = block_blob_service.acquire_blob_lease(el[0], el[1], 15)
    block_blob_service.get_blob_to_path(container_name=el[0], blob_name=el[1], file_path=el[2])
    block_blob_service.release_blob_lease(el[0], el[1], lease_id)

with ThreadPoolExecutor(max_workers=DOP) as executor:
    running_tasks = [executor.submit(pull_srcset_file, item) for item in blobs_to_pull]
    for running_task in running_tasks:
            running_task.result()