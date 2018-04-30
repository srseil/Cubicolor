extends Position3D

export(Color) var color
export(bool) var alive

var requirements = []
var exit
var y_origin
var y
var index

func _ready():
	alive = false

func anim_finished(name):
	if name.begins_with("blend_out"):
		alive = false
		index = requirements.find(self)
		for i in range(index + 1, requirements.size()):
			requirements[i].get_node("anim_player").play("fall_down")
		exit.get_node("anim_player").play("swirl_down")
	elif name.begins_with("blend_in"):
		alive = true
	elif name == "fall_down":
		print("fall_down " + str(color))
		$mesh.translation = Vector3(0, -0.25, 0)
		translate(Vector3(0, -0.5, 0))
		y -= 1
	elif name == "fall_up":
		print("fall_up " + str(color))
		$mesh.translation = Vector3(0, -0.25, 0)
		translate(Vector3(0, 0.5, 0))
		y += 1
		print("     " + str(y) + " " + str(y_origin))
		if y < y_origin:
			$anim_player.play("fall_up")
		#index = requirements.find(self)
		var req = requirements[y - 2]
		if not req.alive:
			req.get_node("anim_player").play(req.get_anim("blend_in"))
	
func reset():
	if not alive:
		translate(Vector3(0, (y_origin - y) * 0.5, 0))
	elif y < y_origin:
		$anim_player.play("fall_up")


func get_anim(name):
	match color:
		global.CUBICOLOR_RED: return name + "_red"
		global.CUBICOLOR_GREEN: return name + "_green"
		global.CUBICOLOR_BLUE: return name + "_blue"
		global.CUBICOLOR_YELLOW: return name + "_yellow"

