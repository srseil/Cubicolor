extends Node

var menu_scene
var level_scene
var menu
var level

var music_player
#var sound_player
var music_muted
var sound_muted

var steam_loaded = false


func _ready():
	# Steam Stuff
	steam_loaded = Steam.steamInit()
	if steam_loaded:
		Steam.requestCurrentStats()
		#Steam.resetAllStats(true)
		#Steam.storeStats()

	menu_scene = load("res://menu.tscn")
	level_scene = load("res://level.tscn")

	music_player = $music_player
	music_player.play()

	menu = menu_scene.instance()
	menu.game = self
	menu.setup_levels()
	add_child(menu, true)

func _process():
	Steam.run_callbacks()

func _notification(what):
	if what == MainLoop.NOTIFICATION_WM_QUIT_REQUEST:
		Steam.shutdown()
		get_tree().quit()

func start_level(diff, number):
	remove_child(get_node("menu"))
	level = level_scene.instance()
	level.game = self
	add_child(level, true)

	for n in get_children():
		print(n.name)

	level.build_level(diff, number)

func close_level():
	remove_child(level)
	level.queue_free()
	add_child(menu, true)

	match menu.current_diff:
		global.NORMAL: menu.normal_pressed()
		global.SMART: menu.smart_pressed()
		global.GENIUS: menu.genius_pressed()

	print("öalskjfaölsdkjföalskjfaölskjfaölskjf")
	print(menu.current_diff)

func switch_level(diff, number):
	remove_child(get_node("level"))
	level.queue_free()
	level = level_scene.instance()
	level.game = self
	add_child(level, true)
	level.build_level(diff, number)

	for n in get_children():
		print(n.name)

