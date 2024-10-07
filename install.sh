#!/bin/bash

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $@"
}

BOT_TOKEN=""
OWNER=""

for arg in "$@"; do
    case $arg in
        BOT_TOKEN=*) BOT_TOKEN="${arg#*=}" ;;
        OWNER=*) OWNER="${arg#*=}" ;;
        *) log "Unknown argument: $arg" ;;
    esac
done

if [ -z "$BOT_TOKEN" ] || [ -z "$OWNER" ]; then
    log "Error: BOT_TOKEN and OWNER must be provided."
    exit 1
fi

log "BOT_TOKEN: [REDACTED]"
log "OWNER: $OWNER"

clean_container() {
    docker stop telegram_bot_container || true
    docker rm telegram_bot_container || true
    docker image rm telegram_bot_image || true
}

build_and_run() {
    cd OrgBot

    clean_container

    docker build -t telegram_bot_image .

    if [ $? -ne 0 ]; then
        log "Docker image build failed."
        exit 1
    fi

    docker run -d --name telegram_bot_container \
        -v $(pwd):/app/data \
        -e BOT_TOKEN="$BOT_TOKEN" \
        -e OWNER="$OWNER" \
        telegram_bot_image

    if [ $? -ne 0 ]; then
        log "Docker container failed to start."
        exit 1
    fi

    docker container prune -f
    docker image prune -f

    sleep 10
    docker container logs telegram_bot_container

    cd ..
}

fetch_updates() {
    git init
    git remote add origin https://github.com/kekal/ModerationBot || true
    git fetch origin
    git reset --hard origin/master
    git clean -df

    chmod +x install.sh
}

# Main

clean_container

while true; do

    exit_code=$(docker inspect telegram_bot_container --format='{{.State.ExitCode}}')

    if [[ -n "$exit_code" ]]; then
        log "Last exit code [$exit_code]."
    else
        log "Container does not exist or cannot retrieve exit code."
        exit_code=42
    fi

    if [ "$exit_code" -eq 42 ]; then
        log "Restarting the deployment"
        fetch_updates
        build_and_run
    elif [ "$exit_code" -eq 1 ]; then
        log "Critical error. Exiting script."
        exit 1
    elif [ "$exit_code" -eq 0 ]; then
        log "Bot exited normally. Cleaning up and exiting script."
        clean_container
        exit 0
    else
        log "Attempting to restart the container in 30 seconds..."
        sleep 30
        docker restart telegram_bot_container
        if [ $? -ne 0 ]; then
            log "Failed to restart the container. Exiting as critical."
            exit 1
        fi
    fi

    log "Waiting for the container to exit..."
    docker wait telegram_bot_container

done
