#!/bin/sh

set -e

VERSION=`git describe --tags --abbrev=0 | awk -F. '{OFS="."; $NF+=1; print $0}'`

DOCKER_TAG="${CR_PREFIX}/xcloud:${VERSION}"

git add .
git commit -m "release ${VERSION}" || true
git tag -a "$VERSION" -m "bump version"
git push

docker build -t "${DOCKER_TAG}" .
docker push "${DOCKER_TAG}"

echo "Version ${VERSION} has been released"
