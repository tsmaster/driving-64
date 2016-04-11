import os
import shutil
import time

SRC_FILENAME = "driving.gif"

DEST_FOLDER = "ScreenShots"
DEST_FILENAME = os.path.join(DEST_FOLDER, time.strftime("driving-%Y-%m-%d-%H:%M.gif"))

shutil.move(SRC_FILENAME, DEST_FILENAME)
