#! /bin/sh

clear
byzanz-record --exec="./driving64.x86_64" -w 800 -h 820 driving.gif
python archiveGif.py
