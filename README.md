# PipeExplorer
A Windows named pipe monitoring GUI. Requires administrator priviledges to run.

## Features

- Displays name and number of active/maximum connections, like [pipelist.exe](https://docs.microsoft.com/en-us/sysinternals/downloads/pipelist) from [SysInternals Suite](https://docs.microsoft.com/en-us/sysinternals/) does, and creation timestamp as a bonus.
- Highlights newly created and removed pipes, like [Process Explorer](https://docs.microsoft.com/en-us/sysinternals/downloads/process-explorer) from SysInternals Suite does.
- Gives hints about well-known pipe names.
- Pipe pinning, allowing to place all the pipes you're interested in together.
- Multilanguage support (English and Russian for now).

![Screenshot!](screenshot.png)

## Known issues

- The NPFS driver doesn't keep timestamps of pipe creation, so the creation timestamp is determined by the time the named pipe is first seen.
- The named pipes that get quickly created and deleted (or deleted and re-created) between scans won't be noticed about at all.
- The ACLs could not be extracted from pipes without a free server end. Also, for the same reason reading of ACLs may disrupt processes trying to connect to the same ACL.

All of those could be fixed by installing a filesystem filter driver which will gather and export this information. Unfortunately, somebody needs to write such driver first.

## TODO

- More/better hints (see the GetHintFor() function in Models/PipeModel.cs).
- Keep selected line in view when many named pipes gets created or deleted.