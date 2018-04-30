extends Position3D

export(int, "NOTHING", "NORMAL", "KEY_TILE", "LOCK_TILE") var type
export(Color) var color
export(bool) var alive



func anim_finished(name):
	if name.begins_with("revive"):
		alive = true
	#elif name.begins_with("die"):
	#	alive = false

func get_anim(name):
	match color:
		global.CUBICOLOR_RED: return name + "_red"
		global.CUBICOLOR_GREEN: return name + "_green"
		global.CUBICOLOR_BLUE: return name + "_blue"
		global.CUBICOLOR_YELLOW: return name + "_yellow"
		_: return name
