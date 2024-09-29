#!/bin/bash

BOT_TOKEN=""
OWNER=""

for arg in "$@"; do
    case $arg in
    BOT_TOKEN=*) BOT_TOKEN="${arg#*=}" ;;
    OWNER=*) OWNER="${arg#*=}" ;;
    *) echo "Unknown argument: $arg" ;;
    esac
done

if [ -z "$BOT_TOKEN" ] || [ -z "$OWNER" ]; then
    echo "Error: BOT_TOKEN and OWNER must be provided."
    exit 1
fi

echo "BOT_TOKEN: [REDACTED]"
echo "OWNER: $OWNER"

clean_container() {
    docker stop telegram_bot_container || true
    docker rm telegram_bot_container || true
}

build_and_run() {

    cd OrgBot

    clean_container

    docker build -t telegram_bot_image .

    if [ $? -ne 0 ]; then
        echo "Docker image build failed."
        exit 1
    fi

    docker run -d --name telegram_bot_container \
    -e BOT_TOKEN="$BOT_TOKEN" \
    -e OWNER="$OWNER" \
    telegram_bot_image

    if [ $? -ne 0 ]; then
        echo "Docker container failed to start."
        exit 1
    fi

    sleep 10
    docker container logs telegram_bot_container

    docker container prune -f
    docker image prune -f

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
    echo "Last exit code [$exit_code]."

    # Upgrade called
    if [ "$exit_code" -eq 42 ] || [ -z "$exit_code" ]; then
        echo "Restarting the deployment"
        fetch_updates

        build_and_run

    elif [ "$exit_code" -eq 0 ] || [ "$exit_code" -eq 1 ]; then
        echo "Exiting."
        exit

    else
        echo "Restarting container..."

        docker stop telegram_bot_container || true
        docker start telegram_bot_container || true

        if [ $? -ne 0 ]; then
            echo "Docker container failed to start."
        fi
    fi

    exit_code=""

    docker wait telegram_bot_container

done
