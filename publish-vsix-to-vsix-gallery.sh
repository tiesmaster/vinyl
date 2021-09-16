#!/bin/bash

publish_url="https://www.vsixgallery.com/api/upload\
?repo=https%3A%2F%2Fgithub.com%2Ftiesmaster%2Fvinyl%2F\
&issuetracker=https%3A%2F%2Fgithub.com%2Ftiesmaster%2Fvinyl%2Fissues%2F\
&readmeUrl=https%3A%2F%2Fraw.githubusercontent.com%2Ftiesmaster%2Fvinyl%2Fmain%2FREADME.md"

curl -i \
    -X POST \
    $publish_url \
    --form file=@src/Vinyl.Vsix/bin/Release/net472/Vinyl.Vsix.vsix