#!/bin/bash

colorNormal(){
  echo -e "\033[0m"
}
colorBold(){
  echo -e "\033[1m$1"
}

colorBold "Checking OS..."
colorBold
if [ "$(expr substr $(uname -s) 1 5)" != "Linux"  ]; then
  colorRed "This script only works on Linux."
  colorNormal
  exit 1
fi
colorBold "Checking for Mono..."
colorNormal
mono -V
if [ $? -eq 0  ]; then
  colorBold "Mono OK"
else
  colorBold "Mono is not installed."
  exit 1
fi

colorBold "Checking for existing client..."

if [ -d "~/.d2moddin/"  ]; then
  colorBold "Found client, checking version..."
  version=`cat ~/.d2moddin/version.txt`
  colorBold "Version $version"
else
  colorBold "Client not found, downloading..."
fi
