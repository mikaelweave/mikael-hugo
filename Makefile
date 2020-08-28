define PROJECT_HELP_MSG
Usage:
    make help               show this message
    make build              make the website
    make deploy-infra		deploy infrastructure for the site and image resizer function
	make deploy-site		deploy website
	make deploy-all 		deploy all 
    make run-local			run the docker container locally
endef
export PROJECT_HELP_MSG

help:
	@echo "$$PROJECT_HELP_MSG" | less

build:
	hugo --minify

deploy-infra: 
	bash scripts/deploy/infrastructure.sh

deploy-site:
	bash scripts/deploy/site.sh