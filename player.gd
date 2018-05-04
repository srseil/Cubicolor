extends KinematicBody

enum {NOTHING, NORMAL, KEY_TILE, LOCK_TILE}
enum {NONE, RED, GREEN, BLUE, YELLOW}

const CUBICOLOR_RED        = Color("#f2777a")
const CUBICOLOR_GREEN      = Color("#99cc99")
const CUBICOLOR_BLUE       = Color("#6699cc")
const CUBICOLOR_YELLOW     = Color("#ffcc66")
const CUBICOLOR_WHITE      = Color("#fcfaf6")
const CUBICOLOR_BLACK      = Color("#2d2d2d")
const CUBICOLOR_BACKGROUND = Color("#f3f1ed")

var level_loaded = false

var level
var exit
var pos = Vector2()
var color
var movable
var fall_ready = false
var paused = false
var completed = false


func _ready():
	color = CUBICOLOR_BLACK
	movable = false
	$mesh_position/mesh.get_surface_material(0).set_shader_param("albedo", Color(0, 0, 0, 0))

func tile_on_grid(x, y):
	# change this to what we had with the C version
	if x < 0 or x >= level.cols or y < 0 or y >= level.rows:
		return null
	else:
		return level.grid[y * level.cols + x]


func _process(delta):
	if Input.is_action_just_pressed("quit"):
		get_tree().quit()

	if Input.is_action_just_pressed("key_pause") \
			or Input.is_action_just_pressed("pad_pause"):
		if not paused:
			level.pause()
			paused = true
		else:
			paused = false
			level.get_node("canvas/CenterContainer/pause_menu").hide()

	paused = level.get_node("canvas/CenterContainer/pause_menu").visible
	if $anim_player.is_playing() or not movable or paused:
		return

	if Input.is_action_just_pressed("key_forwards") \
			or Input.is_action_just_pressed("pad_forwards"):
		var tile = tile_on_grid(pos.x, pos.y - 1)
		if tile != null and tile.alive:
			$anim_player.play("move_up")
		elif tile != exit:
			$anim_player.play("hint_up")
	elif Input.is_action_just_pressed("key_backwards") \
			or Input.is_action_just_pressed("pad_backwards"):
		var tile = tile_on_grid(pos.x, pos.y + 1)
		if tile != null and tile.alive:
			$anim_player.play("move_down")
		elif tile != exit:
			$anim_player.play("hint_down")
	elif Input.is_action_just_pressed("key_right") \
			or Input.is_action_just_pressed("pad_right"):
		var tile = tile_on_grid(pos.x + 1, pos.y)
		if tile != null and tile.alive:
			$anim_player.play("move_right")
		elif tile != exit:
			$anim_player.play("hint_right")
	elif Input.is_action_just_pressed("key_left") \
			or Input.is_action_just_pressed("pad_left"):
		var tile = tile_on_grid(pos.x - 1, pos.y)
		if tile != null and tile.alive:
			$anim_player.play("move_left")
		elif tile != exit:
			$anim_player.play("hint_left")
	elif Input.is_action_just_pressed("key_restart") \
			or Input.is_action_just_pressed("pad_restart"):
		reset()

func reset():
	$anim_player.play(get_anim("fall_up"))
	movable = false
	level.reset()
	level.reset_grid()

func anim_finished(name):
	# with new godot version, use $anim_player.get_playing_speed to check for backwards
	var tile = tile_on_grid(pos.x, pos.y)

	if name == "color" and tile.type == global.KEY_TILE and color != tile.color:
		$mesh_position/mesh.get_surface_material(0) \
			.set_shader_param("albedo", tile.color)
		color = tile.color
		$anim_player.play("color")
		level.get_node("coloring_sound").play()

	if name.begins_with("move"):
		level.get_node("move_sound").play()
		tile.get_node("anim_player").play(tile.get_anim("die"))
		tile.alive = false
		move(name)
		react()

	if name.begins_with("fall_up"):
		translate(Vector3(level.start.x - pos.x, 0, level.start.y - pos.y))
		pos.x = level.start.x
		pos.y = level.start.y
		color = CUBICOLOR_BLACK
		if fall_ready:
			$anim_player.play("fall_down_black")
			fall_ready = false
	elif name.begins_with("fall_down"):
		movable = true


func reset_fall_down():
	if not $anim_player.is_playing():
		$anim_player.play("fall_down_black")
	else:
		fall_ready = true

func move(direction):
	$mesh_position.rotation = Vector3(0, 0, 0)
	if direction == "move_up":
		translate(Vector3(0, 0, -1))
		pos.y -= 1
	elif direction == "move_down":
		translate(Vector3(0, 0, 1))
		pos.y += 1
	elif direction == "move_right":
		translate(Vector3(1, 0, 0))
		pos.x += 1
	elif direction == "move_left":
		translate(Vector3(-1, 0, 0))
		pos.x -= 1


func color():
	pass



func react():
	var tile = tile_on_grid(pos.x, pos.y)
	if tile.type == KEY_TILE:
		if color != CUBICOLOR_BLACK and color != tile.color:
			$anim_player.play_backwards("color")
			level.get_node("decoloring_sound").play()
		elif color == CUBICOLOR_BLACK:
			color = tile.color
			$mesh_position/mesh.get_surface_material(0) \
				.set_shader_param("albedo", tile.color)
			$anim_player.play("color")
			level.get_node("coloring_sound").play()
	elif tile.type == LOCK_TILE:
		if tile.color == color:
			$anim_player.play_backwards("color")
			level.get_node("decoloring_sound").play()
			color = CUBICOLOR_BLACK
			level.remove_requirement(tile.color)
	elif tile == exit:
		print("WINWINWINWINWINWINWINWIN")
		# change the progress
		var dir
		match level.diff:
			global.NORMAL: dir = "application/config/normal_progress"
			global.SMART: dir = "application/config/smart_progress"
			global.GENIUS: dir = "application/config/genius_progress"
		var progress = ProjectSettings.get_setting(dir)
		print("number: " + str(level.number))
		print("progress: " + str(progress))
		if level.number <= progress:
			level.completed()
			return
		ProjectSettings.set_setting(dir, progress + 1)
		if progress + 1 == 16 and level.diff != global.GENIUS:
			match level.diff + 1:
				global.SMART: dir = "application/config/smart_progress"
				global.GENIUS: dir = "application/config/genius_progress"
			if ProjectSettings.get_setting(dir) < 0:
				ProjectSettings.set_setting(dir, 0)
		ProjectSettings.save()
		level.completed()

func move_anim_offset(reverse, axis):
	if reverse:
		rotation = Vector3(0, 0, 0)
		$mesh.translate(axis * 0.5)
		#translate(axis * -0.5)
		translate(axis * 0.5)
		# should not be here but bugs:
		#translate(Vector3(1, 0, 0))
	else:
		translate(axis * 0.5)
		$mesh.translate(axis * -0.5)

func get_anim(name):
	match color:
		global.CUBICOLOR_RED: return name + "_red"
		global.CUBICOLOR_GREEN: return name + "_green"
		global.CUBICOLOR_BLUE: return name + "_blue"
		global.CUBICOLOR_YELLOW: return name + "_yellow"
		global.CUBICOLOR_BLACK: return name + "_black"

