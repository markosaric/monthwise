#!/usr/bin/env bash
# Double-click in Finder (or keep in the Dock) to start Monthwise and open it in the browser.
exec "$(dirname "$0")/monthwise.sh" start --open
