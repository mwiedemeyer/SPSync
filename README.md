# Please note: This project is no longer under active development

# SPSync

This is the source for [SPSync](http://spsync.net), the alternative SharePoint/OneDrive for Business sync tool.

You may find some strange code snippets in here, but that's
because a sync engine is not trivial as we all know from the original official OneDrive for Business aka Groove tool. Also most of the code is already a few years old, so today I would do some things different than they are now.

If you want to try to make it better, feel free and send a pull request.

**NOTE:** With the release of the source for SPSync, I will stop supporting it by mail. If you have a question or found an bug please open an issue here.

## Development
You just need Visual Studio (Community edition is fine). Open the solution and build it. Now you can use
SPSync.exe to run it.

## Structure
The solution contains 4 projects.

- SPSync.Core: The sync engine, SharePoint communication, metadata store, etc
- SPSync: The taskbar icon and UI. Also contains FileWatcher to monitor local changes
- (Optional) SPSyncCli: The command line interface, which uses SPSync.Core
- (Optional) SPSyncTest: Just a small command line to test some things fast during development.
- SquirrelSetup: A folder containing the nuspec file to create a [Squirrel](https://github.com/Squirrel/Squirrel.Windows) setup

## Known Issues
- Sync not working when you change a local file while you're offline
- Sometimes SPSync is deleting files (remote and/or local) unexpectedly
- Moving or cut/paste files is not working as expected all the time
- Sometimes a file/folder is not sync'ed


