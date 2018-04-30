extends Node

# Tile type
enum {NOTHING, NORMAL, KEY_TILE, LOCK_TILE}
# Tile and player color
enum {NONE, RED, GREEN, BLUE, YELLOW}

enum {NORMAL, SMART, GENIUS}

const CUBICOLOR_RED        = Color("#f2777a")
const CUBICOLOR_GREEN      = Color("#99cc99")
const CUBICOLOR_BLUE       = Color("#6699cc")
const CUBICOLOR_YELLOW     = Color("#ffcc66")
const CUBICOLOR_WHITE      = Color("#fcfaf6")
const CUBICOLOR_BLACK      = Color("#2d2d2d")
const CUBICOLOR_BACKGROUND = Color("#f3f1ed")

var global

func _init():
	global = self

# we could make this better by having all scripts inherit from a custom script that itself inherits from Node... no... but then i cant inherit from anything else anymore :(

var diff
var number

