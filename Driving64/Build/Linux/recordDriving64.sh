#! /bin/sh

clear
#byzanz-record --exec="./driving64.x86_64" -x 169 -y 51 -w 1000 -h 1000 driving.gif
byzanz-record --exec="./driving64.x86_64" -x 0 -y 44 -w 800 -h 800 driving.gif
python archiveGif.py
