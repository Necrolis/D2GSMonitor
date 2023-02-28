# D2GSMonitor
A simple yet powerful monitoring tool for D2GS, primarily aimed at windows systems, but should also work with Wine based setups.

Requires .Net 6.0 Framework (Desktop) Runtime, you can get it from [Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/6.0).

## Features
D2GSMonitor provides the following features:
 - Automatic restarting of D2GS
 - Timed restarting of D2GS
 - Deadlock detection of D2GS with forced restarting
 - Automatic file downloads and updating
 - REST+JSON based Push'ing of events (start, restart, failure) & data (games list and status) with optional header for API or basic HTTP auth'ing
 - Automatically sets admin and compatibility requirements (windows only)
 - Automatically sets all required Registry keys (windows only)
 - Register for automatic startup (windows only)
 
 ## Configuration
All needed configuration is done via the `config.json` file. It needs to be in the same directory as D2GSMonitor.

**note that changes will only take effect when you restart D2GSMonitor**

The format of `config.json` is fairly straight-forward. 
Here is the schema with inline documentation: 
```json
{
	/* a nam to uniquely identify this GS instance */
	"gsname": string,
	/* ULR endpoints used to send and get data */
	"endpoints": {
		/* URL to POST game & status data to */
		"data": string|null,
		/* URL to POST events to */
		"events": string|null,
		/* URL to GET the file download list from */
		"manifest": string|null,
		/* URL to GET the registry settings from */
		"registry": string|null
	},
	"auth": {
		/* The name of the HTTP header to use for auth */
		"header": string|null,
		/* The value of the HTTP header to use for auth */
		"value": string|null
	},
	"telnet": {
		/* your local telnet port, 0 to disable */
		"port": int,
		/* your local telnet password */
		"password": string|null
	},
	/* time in minutes before the GS should be automatically restarted */
	"restart_duration": int,
	/* wait time in seconds before the GS is forcefully restarted after a restart command */
	"restart_timeout": int,
	"update": {
		/* update all files using the manifest URL */
		"files": bool,
		/* update the registry using the registry URL */
		"registry": bool
	},
	"report": {
		/* time in seconds between each status report, set to 0 to disable */
		"status": int,
		/* time in seconds between each games report, set to 0 to disable */
		"games": int,
	},
	"watchdog": {
		/* offset of the GE update timer in D2Server.dll */
		"offest": int,
		/* time in milliseconds before the game is considered deadlocked */
		"timeout": int
	},
	/* register to start at system start  */
	"autostart": bool
}
```

## Receiving JSON
All data sent out via web requests by D2GSMonitor is JSON encoded.
The format of the messages can be grok'ed from JSON.cs with `JSON.Event` and `JSON.Data` being the respective top-level JSON schema's.

It is advised to secure your recieve endpoints by setting the `auth_header` and `auth_value` members.
This settings pair can be used to create a number of different HTTP header style authentication methods, from basic HTTP auth to API token based auth.

If you want to pass additional data, you can use URL query parameters as part of each endpoint's URL. 