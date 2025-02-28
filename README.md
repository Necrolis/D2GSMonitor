# D2GSMonitor
A simple yet powerful monitoring tool for D2GS, primarily aimed at windows systems, but should also work with Wine based setups.
By default D2GSMonitor assumes that D2GS.exe is in the same folder, however you can override this behavior in the config file.

Requires .Net 9.0 Framework (Desktop) Runtime, you can get it from [Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).

## Features
D2GSMonitor provides the following features:
 - Automatic restarting of D2GS
 - Timed restarting of D2GS
 - Deadlock detection of D2GS with forced restarting
 - Automatic file downloads and updating
 - REST+JSON based Push'ing of events (start, restart, failure) & data (gamelist and status) with optional header for API or basic HTTP auth'ing
 - Automatically sets admin and compatibility requirements (windows only)
 - Automatically sets all required Registry keys (windows only)
 - Register for automatic startup (windows only)
 
 ## Configuration
All needed configuration is done via the `config.json` file. It must be in the same directory as D2GSMonitor.

**Note that config changes will only take effect when you restart D2GSMonitor**

The format of `config.json` is fairly straight-forward. 
Here is the schema with inline documentation: 
```json
{
	/* a name to uniquely identify this GS instance */
	"gsname": string,
	/* an override path to the D2GS.exe to use */
	"executable": string,	
	/* URL endpoints used to send and retreive data */
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
		/* The name of the HTTP header to use for authentication */
		"header": string|null,
		/* The value of the HTTP header to use for authentication */
		"value": string|null
	},
	"telnet": {
		/* your local telnet port, 0 to disable telnet commands */
		"port": int,
		/* your local telnet password */
		"password": string|null
	},
	/* time in minutes before the GS should be automatically restarted (requires telnet) */
	"restart_duration": int,
	/* wait time in seconds before the GS is forcefully restarted after a restart command was sent */
	"restart_timeout": int,
	"update": {
		/* try update all files using the manifest URL on launch */
		"files": bool,
		/* update the registry using the registry URL on launch */
		"registry": bool
	},
	"report": {
		/* time in seconds between each status report, set to 0 to disable (requires endpoints.data) */
		"status": int,
		/* time in seconds between each games report, set to 0 to disable (requires endpoints.data) */
		"games": int,
	},
	"watchdog": {
		/* offset of the GE update timer in D2Server.dll, 1.13c: 69364 */
		"offest": int,
		/* time in milliseconds before the GE is considered deadlocked */
		"timeout": int
	},
	/* register to start D2GSMonitor at system start */
	"autostart": bool
}
```
`config.json.example` provides a skeleton file for you to copy and edit. 
D2GSMonitor will also create an empty config file if it cannot find one.

## Receiving JSON
All data sent out via web requests by D2GSMonitor is JSON encoded.
The format of the messages can be grok'ed from JSON.cs with `JSON.Event` and `JSON.Data` being the respective top-level JSON schema's.

It is advised to secure your receive endpoints by setting the `auth_header` and `auth_value` members.
The settings pair can be used to create a number of different HTTP header style authentication methods, from basic HTTP authentication to API token based authentication.

If you want to pass additional data, you can use URL query parameters as part of each endpoint's URL (remember to correctly URL encode any special characters).
As an example:
```json
/* in config.json */
{
	"endpoints": {
		"data": "https://my.domain.com/api/receive_data?MyCustomParam=123&AnotherCustomParam=abc"
	}
}
```