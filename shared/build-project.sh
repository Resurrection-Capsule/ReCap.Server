#!/bin/bash


if [ -z "$CONFIG" ]; then
	CONFIG=Release
fi

BIN_DIR=bin/
PUBLISH_DIR=publish/




function safe_delete()
{
	if [ -d "$DELETE_DIR" ]; then
		rm -rf "$DELETE_DIR"
		RM_EXIT=$?
		if [ "$RM_EXIT" -ne 0 ]; then
			sudo rm -rf "$DELETE_DIR"
		fi
	fi
}


function permissions()
{
	if [ -d "$PERMS_DIR" ]; then
		chmod 777 -R "$PERMS_DIR"
		CHMOD_EXIT=$?
		if [ "$CHMOD_EXIT" -ne 0 ]; then
			sudo chmod 777 -R "$PERMS_DIR"
		fi
	fi
}

ARGS="$@"
function build()
{
	PUBLISH_OUTPUT_DIR="$PUBLISH_DIR/$RID"

	dotnet publish \
	--configuration "$CONFIG" \
	--runtime "$RID" \
	-o "$PUBLISH_OUTPUT_DIR" \
	$ARGS
	
	PERMS_DIR="$PUBLISH_OUTPUT_DIR" permissions
}




# Clean out prior builds
DELETE_DIR="$PUBLISH_DIR" safe_delete
PERMS_DIR="$PUBLISH_DIR" permissions




# Windows build
RID="win-x64" build
RID="linux-x64" build
#RID="osx-x64" build
#RID="osx-arm64" build
