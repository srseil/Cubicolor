extends Position3D

var requirements = []
var level
var y

func _ready():
	y = 0


func anim_finished(name):
	if name == "revive":
		y = 1
		level.reset()
	elif name == "fall_up":
		$mesh.translation = Vector3(0, -0.25, 0)
		translate(Vector3(0, 0.5, 0))
		y += 1
		$anim_player.play("swirl_up")
	elif name == "fall_down":
		$mesh.translation = Vector3(0, -0.25, 0)
		translate(Vector3(0, -0.5, 0))
		y -= 1
	elif name == "swirl_up":
		$mesh.rotation = Vector3(0, 0, 0)
		$mesh.translation = Vector3(0, -0.25, 0)
		translate(Vector3(0, 0.5, 0))
		y += 1
		if y < requirements.size() + 1:
			$anim_player.play("swirl_up")
		var req = requirements[y - 2]
		if not req.alive:
			req.get_node("anim_player").play(req.get_anim("blend_in"))
	elif name == "swirl_down":
		$mesh.rotation = Vector3(0, 0, 0)
		$mesh.translation = Vector3(0, -0.25, 0)
		translate(Vector3(0, -0.5, 0))
		y -= 1
		if y == 1:
			$anim_player.play("fall_down")

func reset():
	if y == 0:
		$anim_player.play("fall_up")
	elif y < requirements.size() + 1:
		$anim_player.play("swirl_up")



