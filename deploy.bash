#!/bin/bash
xbuild /p:Configuration=Release ./D2MPMaster/D2MPMaster.csproj
rsync -rav --delete ./D2MPMaster/bin/Release/ master:~/master/
