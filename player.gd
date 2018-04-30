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

var level
var pos = Vector2()
var color
var movable
var fall_ready = false


func _ready():
	color = CUBICOLOR_BLACK
	movable = false
	$anim_player.play(get_anim("fall_down"))

func tile_on_grid(x, y):
	# change this to what we had with the C version
	if x < 0 or x >= level.cols or y < 0 or y >= level.rows:
		return null
	else:
		return level.grid[y * level.cols + x]

var paused = false

func _process(delta):
	if Input.is_action_just_pressed("quit"):
		get_tree().quit()

	if Input.is_action_just_pressed("key_pause") \
			or Input.is_action_just_pressed("pad_pause"):
		print("pause bitch")
		if not paused:
			level.pause()
			paused = true
		else:
			paused = false
			level.get_node("canvas/CenterContainer/pause_menu").hide()

	paused = level.get_node("canvas/CenterContainer/pause_menu").visible
	if $anim_player.is_playing() or not movable or paused:
		return

	if Input.is_action_just_pressed("key_up") \
			or Input.is_action_just_pressed("pad_up"):
		var tile = tile_on_grid(pos.x, pos.y - 1)
		if tile == null or not tile.alive:
			$anim_player.play("hint_up")
		else:
			$anim_player.play("move_up")
	elif Input.is_action_just_pressed("key_down") \
			or Input.is_action_just_pressed("pad_down"):
		var tile = tile_on_grid(pos.x, pos.y + 1)
		if tile == null or not tile.alive:
			$anim_player.play("hint_down")
		else:
			$anim_player.play("move_down")
	elif Input.is_action_just_pressed("key_right") \
			or Input.is_action_just_pressed("pad_right"):
		var tile = tile_on_grid(pos.x + 1, pos.y)
		if tile == null or not tile.alive:
			$anim_player.play("hint_right")
		else:
			$anim_player.play("move_right")
	elif Input.is_action_just_pressed("key_left") \
			or Input.is_action_just_pressed("pad_left"):
		var tile = tile_on_grid(pos.x - 1, pos.y)
		if tile == null or not tile.alive:
			$anim_player.play("hint_left")
		else:
			$anim_player.play("move_left")
	elif Input.is_action_just_pressed("key_reset") \
			or Input.is_action_just_pressed("pad_reset"):
		reset()

func reset():
	$anim_player.play(get_anim("fall_up"))
	movable = false
	level.reset()

func anim_finished(name):
	# with new godot version, use $anim_player.get_playing_speed to check for backwards
	var tile = tile_on_grid(pos.x, pos.y)

	if name == "color" and tile.type == global.KEY_TILE and color != tile.color:
		$mesh_position/mesh.get_surface_material(0) \
			.set_shader_param("albedo", tile.color)
		color = tile.color
		$anim_player.play("color")

	if name.begins_with("move"):
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
		elif color == CUBICOLOR_BLACK:
			color = tile.color
			$mesh_position/mesh.get_surface_material(0) \
				.set_shader_param("albedo", tile.color)
			$anim_player.play("color")
	elif tile.type == LOCK_TILE:
		if tile.color == color:
			$anim_player.play_backwards("color")
			color = CUBICOLOR_BLACK
			level.remove_requirement(tile.color)

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

