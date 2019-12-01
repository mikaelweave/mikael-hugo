#!/usr/bin/env python3

import os, time, random
from azure.storage.blob import BlockBlobService, PublicAccess, baseblobservice
from azure.common import AzureConflictHttpError

from dotenv import load_dotenv, find_dotenv
load_dotenv(find_dotenv())

BLOB_ACCOUNT_NAME = os.getenv("BLOB_ACCOUNT_NAME")
BLOB_ACCOUNT_KEY = os.getenv("BLOB_ACCOUNT_KEY")

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
srcset_file_path = '/../data/srcsets.json'
if not os.path.exists(SCRIPT_DIR + '/../data/'):
    os.makedirs(SCRIPT_DIR + './../data/')

block_blob_service = BlockBlobService(BLOB_ACCOUNT_NAME, BLOB_ACCOUNT_KEY)

while True:
    try:
        lease_id = block_blob_service.acquire_blob_lease("$web", "srcsets.json", 15)
        block_blob_service.get_blob_to_path("$web", "srcsets.json", SCRIPT_DIR + srcset_file_path, lease_id=lease_id)
        block_blob_service.release_blob_lease("$web", "srcsets.json", lease_id)
        break
    except AzureConflictHttpError:
        time.sleep (random.randint(1,3056) / 1000.0);