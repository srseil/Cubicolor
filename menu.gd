extends CanvasLayer

# class member variables go here, for example:
# var a = 2
# var b = "textvar"

var resolutions = ["3840x2160", "2560x1440", "1920x1080", "1680x1050", "1600x900", "1440x900", "1366x768", "1280x1024", "1280x720", "1024x768", "800x600"]

var font

var menu_button_group
var current_menu_button

var current_diff = global.NORMAL

var level_pack = preload("res://level.tscn")
var level

var game

func _ready():
	font = preload("res://fonts/vollkorn_regular_normal.tres")

	var button = \
		$HBoxContainer/panels/settings/VBoxContainer/HBoxContainer4/OptionButton
	for res in resolutions:
		button.add_item(res)

	load_state()
	$HBoxContainer/buttons/credits.grab_focus()

	print("RECURSIVE")
	add_button_sound(self)
		

func add_button_sound(base):
	for node in base.get_children():
		add_button_sound(node)
	if base is Button or base is CheckBox:
		base.connect("pressed", $button_sound, "play", [])

func setup_levels():
	#level = level_pack.instance()
	for container in $HBoxContainer/panels/play/VBoxContainer/CenterContainer/GridContainer.get_children():
		#if "pressed" in container.get_child(0).get_signal_list():
		container.get_child(0).disconnect(
			"pressed", game, "start_level")
		#if not container.get_child(0).is_connected("pressed", game, "start_level"):
		container.get_child(0).connect(
			"pressed", game, "start_level", [current_diff, container.get_index() + 1])

func start_level(diff, number):
	get_tree().change_scene("res://level.tscn")
	print("menu")
	#get_tree().change_scene_to(level_pack)
	#level_pack.build_level(diff, number)

func _process(delta):
	if Input.is_action_just_pressed("bigger"):
		font.size += 1
		print(font.size)
	elif Input.is_action_just_pressed("smaller"):
		font.size -= 1
		print(font.size)

func menu_button_changed(pressed, name):
	$HBoxContainer/panels/play.visible = false
	$HBoxContainer/panels/controls.visible = false
	$HBoxContainer/panels/settings.visible = false
	$HBoxContainer/panels/credits.visible = false
	get_node("HBoxContainer/panels/" + name).visible = true

func quit_pressed():
	get_tree().quit()

# SETUP

# CONTROLS
onready var key_forwards = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer/Button
onready var key_backwards = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer2/Button
onready var key_left = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer3/Button
onready var key_right = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer4/Button
onready var key_restart = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer5/Button
onready var key_pause = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer6/Button

#onready var pad_forwards = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer/Button2
#onready var pad_backwards = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer2/Button2
#onready var pad_left = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer3/Button2
#onready var pad_right = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer4/Button2
#onready var pad_restart = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer5/Button2
#onready var pad_pause = $HBoxContainer/panels/controls/VBoxContainer/HBoxContainer6/Button2

# SETTINGS
onready var vsync = $HBoxContainer/panels/settings/VBoxContainer/HBoxContainer5/CheckBox2
onready var fullscreen = $HBoxContainer/panels/settings/VBoxContainer/HBoxContainer5/CheckBox
onready var resolution = $HBoxContainer/panels/settings/VBoxContainer/HBoxContainer4/OptionButton
onready var mute_music = $HBoxContainer/panels/settings/VBoxContainer/HBoxContainer/VBoxContainer/HSplitContainer/CheckBox
onready var mute_sound = $HBoxContainer/panels/settings/VBoxContainer/HBoxContainer/VBoxContainer2/HSplitContainer/CheckBox
onready var music_slider = $HBoxContainer/panels/settings/VBoxContainer/HBoxContainer/VBoxContainer/HSlider
onready var sound_slider = $HBoxContainer/panels/settings/VBoxContainer/HBoxContainer/VBoxContainer2/HSlider
onready var music_volume = $HBoxContainer/panels/settings/VBoxContainer/HBoxContainer/VBoxContainer/HSplitContainer/Label
onready var sound_volume = $HBoxContainer/panels/settings/VBoxContainer/HBoxContainer/VBoxContainer2/HSplitContainer/Label
onready var dark_background = $HBoxContainer/panels/settings/VBoxContainer/HBoxContainer2/CheckBox2
onready var hide_ui = $HBoxContainer/panels/settings/VBoxContainer/HBoxContainer3/CheckBox

onready var level_grid = $HBoxContainer/panels/play/VBoxContainer/CenterContainer/GridContainer

func get_joy_button_name(id):
	match id:
		0: "A"
		1: "B"
		2: "X"
		3: "Y"
		4: "Left Shoulder"
		5: "Right Shoulder"
		6: "Left Trigger"
		7: "Right Trigger"
		10: "Select"
		11: "Start"
		_: "Unknown"

func load_input_map(name):
	var key_event = InputEventKey.new()
	key_event.echo = false
	key_event.pressed = true
	key_event.scancode = \
		ProjectSettings.get_setting("application/config/" + name)
	InputMap.action_add_event(name, key_event)

func save_input_map(name):
	var key_event = InputMap.get_action_list(name)[0]
	ProjectSettings.set_setting(
		"application/config/" + name, key_event.scancode)
	print(key_event.scancode)
	ProjectSettings.save()

func normal_pressed():
	var progress = \
		ProjectSettings.get_setting("application/config/normal_progress")
	set_level_grid_progress(progress)
	current_diff = global.NORMAL
	setup_levels()
	$HBoxContainer/panels/play/VBoxContainer/HBoxContainer/Button.grab_focus()
	print("jajajajaja")

func smart_pressed():
	var progress = \
		ProjectSettings.get_setting("application/config/smart_progress")
	set_level_grid_progress(progress)
	current_diff = global.SMART
	setup_levels()

func genius_pressed():
	var progress = \
		ProjectSettings.get_setting("application/config/genius_progress")
	set_level_grid_progress(progress)
	current_diff = global.GENIUS
	setup_levels()

func set_level_grid_progress(progress):
	for container in level_grid.get_children():
		var button = container.get_child(0)
		if container.get_index() < progress:
			button.disabled = false
			container.get_node("texture").visible = true
		elif container.get_index() == progress:
			button.disabled = false
			container.get_node("texture").visible = false
		else:
			button.disabled = true
			container.get_node("texture").visible = false

func music_volume_changed(volume):
	music_volume.text = str(volume)
	game.music_player.set_volume_db((1 - volume / 100) * -30)
	ProjectSettings.set_setting("audio/music_volume", volume / 100)
	ProjectSettings.save()

func sound_volume_changed(volume):
	sound_volume.text = str(volume)
	#game.sound_player.set_volume_db((1 - volume / 100) * -40)
	# alle einzeln
	$button_sound.set_volume_db((1 - volume / 100) * -30)
	ProjectSettings.set_setting("audio/sound_volume", volume / 100)
	ProjectSettings.save()

func mute_music_toggled(active):
	game.music_muted = true
	ProjectSettings.set_setting("audio/music_muted", active)
	ProjectSettings.save()
	if active:
		game.music_player.stop()
	else:
		game.music_player.play()

func mute_sound_toggled(active):
	game.sound_muted = true
	ProjectSettings.set_setting("audio/sound_muted", active)
	ProjectSettings.save()

func load_state():
	# SAVESTATE
	normal_pressed()

	# CONTROLS
	load_input_map("key_forwards")
	load_input_map("key_backwards")
	load_input_map("key_left")
	load_input_map("key_right")
	load_input_map("key_restart")
	load_input_map("key_pause")

	key_forwards.text = InputMap.get_action_list("key_forwards")[0].as_text()
	key_backwards.text = InputMap.get_action_list("key_backwards")[0].as_text()
	key_left.text = InputMap.get_action_list("key_left")[0].as_text()
	key_right.text = InputMap.get_action_list("key_right")[0].as_text()
	key_restart.text = InputMap.get_action_list("key_restart")[0].as_text()
	key_pause.text = InputMap.get_action_list("key_pause")[0].as_text()

	# SETTINGS
	dark_background.pressed = ProjectSettings.get_setting(
		"application/config/dark_background")
	hide_ui.pressed = ProjectSettings.get_setting("application/config/hide_ui")

	var music = ProjectSettings.get_setting("audio/music_volume")
	var sound = ProjectSettings.get_setting("audio/sound_volume")
	music_volume.text = str(music * 100)
	sound_volume.text = str(sound * 100)
	music_slider.value = music * 100
	sound_slider.value = sound * 100
	mute_music.pressed = ProjectSettings.get_setting("audio/music_muted")
	mute_sound.pressed = ProjectSettings.get_setting("audio/sound_muted")

	game.music_player.set_volume_db((1 - music) * -30)
	#game.sound_player.set_volume_db((1 - sound) * -40)
	$button_sound.set_volume_db((1 - sound) * -30)
	game.music_muted = mute_music.pressed
	game.sound_muted = mute_sound.pressed
	if mute_music.pressed:
		game.music_player.stop()

	var res_string = \
		str(ProjectSettings.get_setting("display/window/size/width")) \
		+ "x" + str(ProjectSettings.get_setting("display/window/size/height"))
	var res_index = resolutions.find(res_string)
	resolution.select(res_index)

	fullscreen.pressed = \
		ProjectSettings.get_setting("display/window/size/fullscreen")
	vsync.pressed = \
		ProjectSettings.get_setting("display/window/vsync/use_vsync")

# UI TOGGLES

var remapping = false
var remap_button
var remap_action

func _input(event):
	if event is InputEventMouseButton and remapping:
		for button in [key_forwards, key_backwards, key_left, \
				key_right, key_restart, key_pause]:
			if button.text == "?":
				button.text = \
					InputMap.get_action_list(remap_action)[0].as_text()
				button.pressed = false
				remapping = false
				break
 
	if Input.is_action_just_pressed("key_forwards"):
		print("forwards")
	if Input.is_action_just_pressed("key_backwards"):
		print("backwards")
	if Input.is_action_just_pressed("key_left"):
		print("left")
	if Input.is_action_just_pressed("key_right"):
		print("right")
	if Input.is_action_just_pressed("key_restart"):
		print("restart")
	if Input.is_action_just_pressed("key_pause"):
		print("pause")

	if (not remapping) or (not event is InputEventKey) or (not event.pressed):
		return

	for ev in InputMap.get_action_list(remap_action):
		InputMap.action_erase_event(remap_action, ev)
	InputMap.action_add_event(remap_action, event)
	save_input_map(remap_action)
	remap_button.text = OS.get_scancode_string(event.scancode)
	remap_button.pressed = false
	remapping = false

func forwards_pressed():
	key_forwards.text = "?"
	remap_button = key_forwards
	remap_action = "key_forwards"
	remapping = true

func backwards_pressed():
	key_backwards.text = "?"
	remap_button = key_backwards
	remap_action = "key_backwards"
	remapping = true

func left_pressed():
	key_left.text = "?"
	remap_button = key_left
	remap_action = "key_left"
	remapping = true

func right_pressed():
	key_right.text = "?"
	remap_button = key_right
	remap_action = "key_right"
	remapping = true

func restart_pressed():
	key_restart.text = "?"
	remap_button = key_restart
	remap_action = "key_restart"
	remapping = true

func pause_pressed():
	key_pause.text = "?"
	remap_button = key_pause
	remap_action = "key_pause"
	remapping = true

func dark_background_toggled(active):
	ProjectSettings.set_setting("application/config/dark_background", active)
	ProjectSettings.save()

func hide_ui_toggled(active):
	ProjectSettings.set_setting("application/config/hide_ui", active)
	ProjectSettings.save()

func resolution_selected(id):
	var split = resolutions[id].split("x")
	var width = int(split[0])
	var height = int(split[1])
	print(width)
	print(height)
	ProjectSettings.set_setting("display/window/size/width", width)
	ProjectSettings.set_setting("display/window/size/height", height)
	ProjectSettings.save()

func fullscreen_toggled(active):
	ProjectSettings.set_setting("display/window/size/fullscreen", active)
	ProjectSettings.save()

func vsync_toggled(active):
	ProjectSettings.set_setting("display/window/vsync/use_vsync", active)
	ProjectSettings.save()


