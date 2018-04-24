extends Node

var grid
var rows
var cols
var start
var exit
var requirements

var Tile = preload("res://tile.tscn")

func _ready():
	#later load the levels from file
	grid = []
	rows = 4
	cols = 5
	start = Vector2(4, 1)
	exit = Vector2(0, 0)
	requirements = []

	for i in range(0, rows):
		for j in range(0, cols):
			var tile = Tile.instance()
			tile.translate(Vector3(j, 0, i))
			grid.append(tile)
			$tiles.add_child(tile, true)

#func _process(delta):
#	# Called every frame. Delta is time since last frame.
#	# Update game logic here.
#	pass
