# Vezax Analysis

This CLI tool runs an analysis to find who splashed how much damage from Mark of the Faceless and healed the boss for how much.

It is intended for 25 man and does not yet support 10 mans

## How to Run

Download the latest release here and extract the zip file (source code not required): https://github.com/PhilipKolar/VezaxAnalysis/releases/tag/v1.0.0

The simplest way to run it, in a terminal:

`.\VezaxAnalysis.exe --apiKey YourApiKey --logId JtcX4CmqNrk1yRMT`

* **apiKey**: You need to go to warcraft logs to generate this for yourself. Log in and proceed to your warcraft logs page [here](https://classic.warcraftlogs.com/profile), put any name into "V1 Client Name" (it doesn't matter), and click Set. The value for "V1 Client Key" is what you want, which should appear after you set a client name
* **logId**: This is your log ID from Warcraft Logs. Go to the log you want to run this analysis on and copy it from the address bar in your browser. For example, if your log is https://classic.warcraftlogs.com/reports/JtcX4CmqNrk1yRMT then your logId would be JtcX4CmqNrk1yRMT

## Extra Options (Optional)

* **-h**: Show a list of all the different arguments (similar to here)
* **--onlyKill**: By default the analysis will include all wipes and sum up the healing done and friendly fire together. With this flag set only the successful kill attempt will be included.
* **--wipeGracePeriod <wipeGracePeriod>**: Default: 15. Time in seconds. Removes the last few seconds from the fight on all wipe attempts (NOT the kill attempt). A basic way of accounting for people stacking up to wipe up fast. Note: This parameter does not do anything if --onlyKill is set.
* **--outputFile <outputFile>**: Default: Does not output to file. The file name and/or file path to write a CSV file to with the results. They will still be printed to the console, but also uploaded to a file in a csv format.

## Usage Example

This uses all parameters. Note only apiKey and logId are required.

`.\VezaxAnalysis.exe --apiKey YourApiKey --logId JtcX4CmqNrk1yRMT --onlyKill --wipeGracePeriod 20 --outputFile "C:\Temp\myVezaxResults.csv"`

## Sample Output

This is a sample of the console output generated

```
*** GENERAL VEZAX ***
Player      | # Debuffs | Friendly Fire Dealt (pre-mit) | Vezax Healed (pre-reductions)
------------+-----------+-------------------------------+------------------------------
Shadowmantle| 3         | 295,000                       | 5,900,000
Devino      | 1         | 270,000                       | 5,400,000
Funch       | 1         | 220,000                       | 4,400,000
Goatwilson  | 3         | 220,000                       | 4,400,000
Raviann     | 2         | 175,000                       | 3,500,000
Cowzgomoo   | 1         | 150,000                       | 3,000,000
Tabmow      | 2         | 140,000                       | 2,800,000
Lgs         | 3         | 100,000                       | 2,000,000
Omnific     | 1         | 60,000                        | 1,200,000
Mallers     | 1         | 25,000                        | 500,000
Booth       | 2         | 5,000                         | 100,000
```

