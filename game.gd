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

func load_progress():
	var file = File.new()
	file.open("user://progress.bin", File.READ)
	var normal = file.get_8()
	print(normal)
	if normal == 255:
		normal = -1
		
	var smart = file.get_8()
	if smart == 255:
		smart = -1
		
	var genius = file.get_8()
	if genius == 255:
		genius = -1
	file.close()
	
	ProjectSettings.set_setting("application/config/normal_progress", normal)
	ProjectSettings.set_setting("application/config/smart_progress", smart)
	ProjectSettings.set_setting("application/config/genius_progress", genius)
	
func save_progress():
	var normal = ProjectSettings.get_setting("application/config/normal_progress")
	var smart = ProjectSettings.get_setting("application/config/smart_progress")
	var genius = ProjectSettings.get_setting("application/config/genius_progress")
	print("SAVE")
	print(normal)
	
	var file = File.new()
	file.open("user://progress.bin", File.WRITE)
	file.store_8(normal)
	file.store_8(smart)
	file.store_8(genius)
	file.close()

func _ready():
	# Steam Stuff
	steam_loaded = Steam.steamInit()
	if steam_loaded:
		Steam.requestCurrentStats()
		#Steam.resetAllStats(true)
		Steam.storeStats()
	
	var file = File.new()
	if not file.file_exists("user://progress.bin"):
		print("Creating save file...")
		file.open("user://progress.bin", File.WRITE)
		file.store_8(0)
		file.store_8(-1)
		file.store_8(-1)
		file.close()
	load_progress()

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
	var a = OS.get_ticks_msec()
	remove_child(get_node("menu"))
	level = level_scene.instance()
	level.game = self
	add_child(level, true)
	
	level.build_level(diff, number)
	var b = OS.get_ticks_msec() - a
	print("TIME: " + str(b))

func close_level():
	remove_child(level)
	level.queue_free()
	add_child(menu, true)

	match menu.current_diff:
		global.NORMAL: menu.normal_pressed()
		global.SMART: menu.smart_pressed()
		global.GENIUS: menu.genius_pressed()

func switch_level(diff, number):
	remove_child(get_node("level"))
	level.queue_free()
	level = level_scene.instance()
	level.game = self
	add_child(level, true)
	level.build_level(diff, number)

