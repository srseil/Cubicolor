extends Node

func _process(delta):
	if Input.is_action_just_pressed("debug_pause"):
		get_tree().paused = not get_tree().paused

