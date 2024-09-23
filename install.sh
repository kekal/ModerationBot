#!/bin/bash

# Default values
BOT_TOKEN=""
OWNER=""

# Parse arguments
for arg in "$@"; do
  case $arg in
    BOT_TOKEN=*) BOT_TOKEN="${arg#*=}" ;;
    OWNER=*) OWNER="${arg#*=}" ;;
    *) echo "Unknown argument: $arg" ;;
  esac
done

# Validate required arguments
if [ -z "$BOT_TOKEN" ] || [ -z "$OWNER" ]; then
  echo "Error: BOT_TOKEN and OWNER must be provided."
  exit 1
fi

echo "BOT_TOKEN: [REDACTED]"
echo "OWNER: $OWNER"

# Function to build and run the Docker container
build_and_run() {

  cd OrgBot
  
  # Stop and remove any existing container
  docker stop telegram_bot_container || true
  docker rm telegram_bot_container || true

  # Build the Docker image
  docker build -t telegram_bot_image .

  # Check if the build was successful
  if [ $? -ne 0 ]; then
      echo "Docker image build failed."
      exit 1
  fi

  # Run the Docker container with environment variables for bot token and owner
  docker run -d --name telegram_bot_container \
    -e BOT_TOKEN="$BOT_TOKEN" \
    -e OWNER="$OWNER" \
    telegram_bot_image

  # Check if the container started successfully
  if [ $? -ne 0 ]; then
      echo "Docker container failed to start."
      exit 1
  fi

  # Remove dangling images and containers
  docker container prune -f
  docker image prune -f

  echo "Deployment completed successfully."

  cd ..
}

# Function to fetch updates from GitHub
fetch_updates() {
  git init
  git remote add origin https://github.com/kekal/ModerationBot || true
  git fetch origin
  git reset --hard origin/master
  # git clean -xdf
}

# Main script logic
while true; do
  # Fetch updates from GitHub, including the script itself
  fetch_updates

  # Build and run the bot
  build_and_run
  
  # Wait for the container to exit
  echo "Waiting for the bot to exit..."
  docker wait telegram_bot_container

  # Get the exit code
  exit_code=$(docker inspect telegram_bot_container --format='{{.State.ExitCode}}')

  # Check if the exit code indicates a restart
  if [ "$exit_code" -eq 42 ]; then
      echo "Service restart requested."

      # Restart the script with the same arguments
      echo "Restarting the deployment script..."
      exec "$0" "$@"
  else
      echo "Bot exited with code $exit_code."
      exit $exit_code
  fi
done
