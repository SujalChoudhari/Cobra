DOTNET=/home/sujal/.dotnet/dotnet
SOURCE_FILE=/home/sujal/Code/Cobra/example.cb

RELEASE_OUTPUT=/home/sujal/Code/Cobra/dist/build
RELEASE_APP=$(RELEASE_OUTPUT)/build.app

DEBUG_OUTPUT=/home/sujal/Code/Cobra/dist/debug
DEBUG_APP=$(DEBUG_OUTPUT)/debug.app

.PHONY: release debug run-release run-debug clean

release:
	@mkdir -p $(RELEASE_OUTPUT)
	$(DOTNET) run -- $(SOURCE_FILE) -o $(RELEASE_APP) -k

run-release: release
	$(RELEASE_APP)

debug:
	@mkdir -p $(DEBUG_OUTPUT)
	$(DOTNET) run -- $(SOURCE_FILE) -o $(DEBUG_APP) -k -V 

run-debug: debug
	$(DEBUG_APP)

clean:
	rm -rf $(RELEASE_OUTPUT) $(DEBUG_OUTPUT)