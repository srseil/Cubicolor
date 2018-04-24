extends KinematicBody

func _ready():
	pass

func _process(delta):
	if Input.is_action_just_pressed("quit"):
		get_tree().quit()

	if $anim_player.is_playing():
		return

	if Input.is_action_just_pressed("ui_up"):
		$anim_player.play("move_up")
	elif Input.is_action_just_pressed("ui_down"):
		$anim_player.play("move_down")
	elif Input.is_action_just_pressed("ui_right"):
		$anim_player.play("move_right")
	elif Input.is_action_just_pressed("ui_left"):
		$anim_player.play("move_left")

func move_finished(name):
	if name == "move_up":
		$mesh_position.rotation = Vector3(0, 0, 0)
		translate(Vector3(0, 0, -1))
	elif name == "move_down":
		$mesh_position.rotation = Vector3(0, 0, 0)
		translate(Vector3(0, 0, 1))
	elif name == "move_right":
		$mesh_position.rotation = Vector3(0, 0, 0)
		translate(Vector3(1, 0, 0))
	elif name == "move_left":
		$mesh_position.rotation = Vector3(0, 0, 0)
		translate(Vector3(-1, 0, 0))


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

