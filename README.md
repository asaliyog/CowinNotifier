# CowinNotiofier
 
steps to run-

1-donwload and install https://dotnet.microsoft.com/download/dotnet/3.1

2-clone this repository

3-create a temp gmail account and allow access to less secure apps - https://myaccount.google.com/u/2/lesssecureapps

4- Add username and password of that account into appSettings.json file (this is needed to send emails from the code)

5- run "dotnet build" inside cloned folder 

6- run "cd VSlotNotification/bin/Debug/netcoreapp3.1"

7-run "dotnet VSlotNotification.dll"


there are two functions one with with Cowin public apis and other with UI automation of cowin webpage. this utility uses Cowin api function as default. 
