define PROJECT_HELP_MSG
Usage:
    make help               show this message
    make build              make the website
    make deploy-infra       deploy infrastructure for the site and image resizer function
    make deploy-site        deploy website
    make deploy-all         deploy all 
    make run                run the site locally
endef
export PROJECT_HELP_MSG

help:
	@echo "$$PROJECT_HELP_MSG" | less

build:
	python3 scripts/pull_code_files.py
	hugo --minify --environment production

deploy-infra: 
	bash scripts/deploy/infrastructure.sh

deploy-site:
	bash scripts/deploy/site.sh

deploy-all:
	bash scripts/deploy/infrastructure.sh
	bash scripts/deploy/site.sh

pull-images:
	python3 scripts/pull_images.py

push-images:
	python3 scripts/push_images.py

pull_srcsets_files:
	python3 scripts/pull_srcsets_files.py

run:
	python3 scripts/pull_code_files.py
	hugo serve -D
