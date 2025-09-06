extends Node

enum {NORMAL, SMART, GENIUS}
enum {NOTHING, FLOOR, EXIT, KEY_RED, LOCK_RED, KEY_GREEN, LOCK_GREEN,
	KEY_BLUE, LOCK_BLUE, KEY_YELLOW, LOCK_YELLOW}
enum {REQ_NONE, REQ_RED, REQ_GREEN, REQ_BLUE, REQ_YELLOW}

#const CUBICOLOR_RED        = Color(242, 119, 122, 255)
const CUBICOLOR_RED        = Color("#f2777a")
#const CUBICOLOR_GREEN      = Color(153, 204, 153, 255)
const CUBICOLOR_GREEN      = Color("#99cc99")
#const CUBICOLOR_BLUE       = Color(102, 153, 204, 255)
const CUBICOLOR_BLUE       = Color("#6699cc")
#const CUBICOLOR_YELLOW     = Color(255, 204, 102, 255)
const CUBICOLOR_YELLOW     = Color("#ffcc66")
#const CUBICOLOR_WHITE      = Color(252, 250, 246, 255)
const CUBICOLOR_WHITE      = Color("#fcfaf6")
#const CUBICOLOR_BLACK      = Color( 45,  45,  45, 255)
const CUBICOLOR_BLACK      = Color("#2d2d2d")
#const CUBICOLOR_BACKGROUND = Color(243, 241, 237, 255)
const CUBICOLOR_BACKGROUND = Color("#f3f1ed")

var game

var grid = []
var rows
var cols
var start = Vector2()
#var exit
var requirements = []
var exit_node

var diff = SMART
var number = 10

var Tile = preload("res://tile.tscn")

func _process(delta):
	if Input.is_action_just_pressed("next"):
		if number == 16:
			diff = diff + 1 if diff < GENIUS else NORMAL
			number = 1
		else:
			number += 1
		reload()
	elif Input.is_action_just_pressed("previous"):
		if number == 1:
			diff = diff - 1 if diff > NORMAL else GENIUS
			number = 16
		else:
			number -= 1
		reload()

func reload():
	for child in $requirements.get_children():
		child.queue_free()
	for child in $tiles.get_children():
		child.queue_free()
	grid.clear()
	requirements.clear()
	build_level(diff, number)
	print(requirements.size())

func _ready():
	$player.set_process(false)
	set_process(false)

	$canvas/CenterContainer.rect_min_size = get_viewport().size
	$canvas/CenterContainer.rect_size = get_viewport().size
	print($canvas/CenterContainer.rect_size)
	$canvas/CenterContainer.minimum_size_changed()

	var sound_volume = ProjectSettings.get_setting("audio/sound_volume")
	$button_sound.set_volume_db((1 - sound_volume) * -30)
	$coloring_sound.set_volume_db((1 - sound_volume) * -30)
	$decoloring_sound.set_volume_db((1 - sound_volume) * -30)
	$solve_sound.set_volume_db((1 - sound_volume) * -30)
	$move_sound.set_volume_db((1 - sound_volume) * -30)
	$revive_sound.set_volume_db((1 - sound_volume) * -30)

	add_button_sound(self)
	var dark = ProjectSettings.get_setting("application/config/dark_background")
	if dark:
		print("DARK")
		$environment.environment.background_color = \
			Color(0.224, 0.224, 0.224, 1.0)
		$canvas/difficulty.add_color_override("font_color", Color("#f2f0ed"))
		$canvas/level.add_color_override("font_color", Color("#f2f0ed"))
		$canvas/tutorial1.add_color_override("font_color", Color("#f2f0ed"))
		$canvas/tutorial2.add_color_override("font_color", Color("#f2f0ed"))
		$canvas/tutorial_first/Label.add_color_override(
			"font_color", Color("#f2f0ed"))
		$canvas/tutorial_first/Label2.add_color_override(
			"font_color", Color("#f2f0ed"))
		$canvas/tutorial_first/Label3.add_color_override(
			"font_color", Color("#f2f0ed"))
		$canvas/tutorial_first/TextureRect.texture = \
			preload("res://gui/arrow_up_dark.png")
		$canvas/tutorial_first/TextureRect2.texture = \
			preload("res://gui/arrow_down_dark.png")
		$canvas/tutorial_first/TextureRect3.texture = \
			preload("res://gui/arrow_up_dark.png")
	else:
		print("BRIGHT")
		$environment.environment.background_color = Color("#f2f0ed")
	
	var hide_ui = ProjectSettings.get_setting("application/config/hide_ui")
	if hide_ui:
		$canvas/difficulty.hide()
		$canvas/level.hide()
	
	$canvas/tutorial_first.rect_size.y = get_viewport().size.y
	$canvas/tutorial_first.rect_size.x = get_viewport().size.y * (4.0 / 3.0)
	$canvas/tutorial_first.rect_position.x = \
		(get_viewport().size.x - $canvas/tutorial_first.rect_size.x) / 2.0


func add_button_sound(base):
	for node in base.get_children():
		add_button_sound(node)
	if base is Button or base is CheckBox:
		base.connect("pressed", $button_sound, "play", [])

func build_level(diff, number):
	self.diff = diff
	self.number = number
	load_level(diff, number)

	match diff:
		NORMAL: $canvas/difficulty.text = "Normal"
		SMART: $canvas/difficulty.text = "Smart"
		GENIUS: $canvas/difficulty.text = "Genius"
	$canvas/level.text = "Level " + str(number)

	$player.translate(Vector3(start.x, 0, start.y))
	$player.pos = start
	$player.level = self
	$player.exit = exit_node

	$camera.translation = Vector3(0, 0, 0)
	$camera.rotation = Vector3(0, 0, 0)
	$camera.translate(Vector3(
		(cols - 1) / 2.0, 4.0, (rows - 1) / 2.0))
	var distance = $camera.translation.y / tan(deg2rad(60))
	if not (diff == global.NORMAL and number == 1):
		var a = $exit_tile.transform.origin.x \
			+ $exit_tile.transform.origin.z + 1
		distance -= requirements.size() / (a * 5.0)
		#distance -= requirements.size() / 3.0
	$camera.translate(Vector3(
		sin(deg2rad(20)) * distance, 0, sin(deg2rad(70)) * distance))
	$camera.rotate_x(deg2rad(-60))
	$camera.rotate_y(deg2rad(20))
	# when requirement staple is very high, maybe go further back??

	$camera.size = rows + 2.5 + 0.25
	if $camera.size < 6:
		$camera.size = 6



	# position might actually be important???
	$directional_light.look_at(Vector3(-0.15, -1, -0.6), Vector3(0, 1, 0))
	#print($directional_light.transform)

	$exit_tile.get_node("anim_player").play("revive")
	reset_grid()

	var hide_ui = ProjectSettings.get_setting("application/config/hide_ui")
	if not hide_ui and diff == global.NORMAL and number == 1:
		$canvas/tutorial_first.visible = true
	elif not hide_ui and diff == global.NORMAL and number == 2:
		$canvas/tutorial1.visible = true
	elif not hide_ui and diff == global.NORMAL and number == 3:
		$canvas/tutorial2.visible = true

	set_process(true)
	$player.set_process(true)


func remove_requirement(color):
	for t in requirements:
		print(t)

	var req
	for i in range(requirements.size() - 1, -1, -1):
		if requirements[i].color == color and requirements[i].alive:
			req = requirements[i]
			break
	if req == null:
		return
	req.get_node("anim_player").play(req.get_anim("blend_out"))
	
	for r in requirements:
		if r.alive and r != req:
			print("still alive")
			return
	exit_node.type = global.FLOOR
	exit_node.alive = true

func reset():
	for req in requirements:
		req.reset()
	$exit_tile.reset()
	#exit_node.type = global.NOTHING
	#exit_node.alive = false
	#reset_grid()


func reset_grid():
	var wait_time = 0
	for tile in grid:
		print(str(tile.alive) + " " + str(tile.type))
		if tile.type != global.NOTHING and not tile.alive:
			var this_time = 0.1 * tile.translation.x \
				+ 0.2 * tile.translation.z
			if this_time == 0:
				tile.get_node("anim_player").play(tile.get_anim("revive"))
				continue

			var timer = Timer.new()
			tile.add_child(timer)
			timer.one_shot = true
			timer.wait_time = this_time
			if wait_time < timer.wait_time:
				print(str(wait_time) + " < " + str(timer.wait_time))
				wait_time = timer.wait_time
			timer.connect("timeout", tile.get_node("anim_player"),
				"play", [tile.get_anim("revive")], CONNECT_ONESHOT)
			timer.start()

	var timer = Timer.new()
	add_child(timer)
	timer.one_shot = true
	timer.wait_time = wait_time + 0.77
	#+ grid[0].get_node("anim_player").get_animation("revive").length
	timer.connect("timeout", $player, "reset_fall_down", [], CONNECT_ONESHOT)
	timer.start()

func load_level(difficulty, number):
	var filename = "res://levels/"
	match difficulty:
		NORMAL: filename += "normal"
		SMART: filename += "smart"
		GENIUS: filename += "genius"
	if number < 10:
		filename += "0"
	filename += str(number) + ".bin"

	print(filename)

	var file = File.new()
	file.open(filename, file.READ)

	# row, column
	start.y = file.get_8()
	start.x = file.get_8()

	for i in range(8):
		var id = file.get_8()
		if id == 0:
			continue

		var tile = Tile.instance()
		tile.set_script(preload("res://requirement.gd"))
		#tile.get_node("anim_player").connect("animation_finished", tile, 

		var material_name
		match id:
			REQ_RED:
				material_name = "tile_red"
				tile.color = CUBICOLOR_RED
			REQ_GREEN:
				material_name = "tile_green"
				tile.color = CUBICOLOR_GREEN
			REQ_BLUE:
				material_name = "tile_blue"
				tile.color = CUBICOLOR_BLUE
			REQ_YELLOW:
				material_name = "tile_yellow"
				tile.color = CUBICOLOR_YELLOW
		tile.get_node("mesh").set_surface_material(0,
			load("res://materials/" + material_name + ".tres").duplicate())
		#tile.get_node("mesh").get_surface_material(0).albedo_color.a = 0;
		tile.get_node("mesh").get_surface_material(0).set_shader_param("albedo", Color(1, 1, 1, 0))

		requirements.push_front(tile)
		$requirements.add_child(tile, true)

		
	for i in range(requirements.size()):
		requirements[i].translate(Vector3(0, (i + 1) * 0.5, 0))
		requirements[i].y_origin = i + 1
		requirements[i].y = i + 1
		requirements[i].requirements = requirements
		requirements[i].exit = $exit_tile


	$exit_tile.get_node("mesh").set_surface_material(0,
		load("res://materials/exit_tile.tres").duplicate())
	#$exit_tile.get_node("mesh").get_surface_material(0).albedo_color.a = 0
	$exit_tile.get_node("mesh").get_surface_material(0).set_shader_param("albedo", Color(0, 0, 0, 0))
	$exit_tile.requirements = requirements
	$exit_tile.level = self
			
	rows = file.get_8()
	cols = file.get_8()

	# -60 20 0, 3 3 3 

	for i in range(rows * cols):
		var id = file.get_8()

		var tile = Tile.instance()
		tile.translate(Vector3(i % cols, 0, int(i / cols)))
		tile.alive = false
		grid.append(tile)
		$tiles.add_child(tile, true)

		if id == NOTHING or id == EXIT:
			tile.type = global.NOTHING
			tile.visible = false
			if id == EXIT:
				var v = Vector3(i % cols, 0, int(i / cols))
				$exit_tile.translate(v)
				for req in requirements:
					req.translate(v)
				exit_node = tile
			continue

		match id:
			FLOOR:
				tile.type = global.FLOOR
			KEY_RED, KEY_GREEN, KEY_BLUE, KEY_YELLOW:
				tile.type = global.KEY_TILE
			LOCK_RED, LOCK_GREEN, LOCK_BLUE, LOCK_YELLOW:
				tile.type = global.LOCK_TILE

		var material_name
		match id:
			FLOOR:
				material_name = "tile_white"
				tile.color = CUBICOLOR_WHITE
			KEY_RED:
				material_name = "keytile_red"
				tile.color = CUBICOLOR_RED
			LOCK_RED:
				material_name = "tile_red"
				tile.color = CUBICOLOR_RED
			KEY_GREEN:
				material_name = "keytile_green"
				tile.color = CUBICOLOR_GREEN
			LOCK_GREEN:
				material_name = "tile_green"
				tile.color = CUBICOLOR_GREEN
			KEY_BLUE:
				material_name = "keytile_blue"
				tile.color = CUBICOLOR_BLUE
			LOCK_BLUE:
				material_name = "tile_blue"
				tile.color = CUBICOLOR_BLUE
			KEY_YELLOW:
				material_name = "keytile_yellow"
				tile.color = CUBICOLOR_YELLOW
			LOCK_YELLOW:
				material_name = "tile_yellow"
				tile.color = CUBICOLOR_YELLOW
		if material_name != null:
			tile.get_node("mesh").set_surface_material(0,
				load("res://materials/" + material_name + ".tres").duplicate())

		if [KEY_RED, KEY_GREEN, KEY_BLUE, KEY_YELLOW].has(id):
			tile.get_node("mesh").get_surface_material(0).set_shader_param(
				"albedo", Color(0, 0, 0, 0))
			pass
		else:
			#tile.get_node("mesh").get_surface_material(0).albedo_color.a = 0
			tile.get_node("mesh").get_surface_material(0).set_shader_param("albedo", Color(1, 1, 1, 0))
			


	file.close()

func complete_next_pressed():
	game.switch_level(diff, number + 1)

func pause_resume_pressed():
	$canvas/CenterContainer/pause_menu.hide()
	$player.paused = false

func pause_restart_pressed():
	$canvas/CenterContainer/pause_menu.hide()
	$canvas/CenterContainer/complete_menu.hide()
	$canvas/CenterContainer/complete_menu_alt.hide()
	$player.paused = false
	$player.reset()

func pause_menu_pressed():
	$canvas/CenterContainer/pause_menu.hide()
	#get_tree().change_scene("res://menu.tscn")
	game.close_level()

func pause():
	$canvas/CenterContainer/pause_menu.popup()

func completed():
	$solve_sound.play()
	game.save_progress()
	#$player.completed = true # not working, popup auto closes on esc
	$player.paused = true
	if number == 16:
		$canvas/CenterContainer/complete_menu_alt.popup()
		if game.steam_loaded:
			match diff:
				NORMAL: Steam.setAchievement("COMPLETE_NORMAL")
				SMART: Steam.setAchievement("COMPLETE_SMART")
				GENIUS: Steam.setAchievement("COMPLETE_GENIUS")
			Steam.storeStats()
	else:
		$canvas/CenterContainer/complete_menu.popup()


