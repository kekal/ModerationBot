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

build_and_run() {

  cd OrgBot
  
  docker stop telegram_bot_container || true
  docker rm telegram_bot_container || true

  docker build -t telegram_bot_image .

  if [ $? -ne 0 ]; then
      echo "Docker image build failed."
      exit 1
  fi

  docker run -d --name telegram_bot_container \
    -e BOT_TOKEN="$BOT_TOKEN" \
    -e OWNER="$OWNER" \
    telegram_bot_image

  docker container prune -f
  docker image prune -f

  if [ $? -ne 0 ]; then
      echo "Docker container failed to start."
      exit 1
  fi

  sleep 10
  docker container logs telegram_bot_container

  echo "Deployment completed successfully." 

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
while true; do
  fetch_updates

  build_and_run
  
  echo "Waiting for the bot to exit..."
  docker wait telegram_bot_container

  exit_code=$(docker inspect telegram_bot_container --format='{{.State.ExitCode}}')

  # Upgrade called
  if [ "$exit_code" -eq 42 ]; then
      echo "Service restart requested."

      echo "Restarting the deployment script..."
      exec "$0" "$@"
  else
      echo "Bot exited with code $exit_code."
      exit $exit_code
  fi

done
