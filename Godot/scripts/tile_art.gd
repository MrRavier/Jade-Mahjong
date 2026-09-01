class_name TileArt
extends RefCounted

const WIDTH := 72
const HEIGHT := 96

const INK := Color("17352f")
const JADE := Color("176b59")
const JADE_LIGHT := Color("4aa88a")
const JADE_DARK := Color("082f2a")
const GOLD := Color("d6aa43")
const GOLD_LIGHT := Color("ffe39a")
const IVORY := Color("f2e7c8")
const IVORY_SHADOW := Color("bfae82")
const RED := Color("ad382d")
const BLUE := Color("265d7d")

var _cache: Dictionary = {}


func texture_for(kind: int, free := true, selected := false) -> Texture2D:
	var key := "%d:%s:%s" % [kind, free, selected]
	if not _cache.has(key):
		_cache[key] = ImageTexture.create_from_image(_make_tile(kind, free, selected))
	return _cache[key]


func _make_tile(kind: int, free: bool, selected: bool) -> Image:
	var image := Image.create(WIDTH, HEIGHT, false, Image.FORMAT_RGBA8)
	image.fill(Color(0, 0, 0, 0))
	# Deep pixel shadow and carved jade sidewall.
	_rect(image, 6, 8, 64, 86, Color(0.015, 0.055, 0.047, 0.84))
	_rect(image, 4, 5, 64, 86, JADE_DARK)
	_rect(image, 4, 5, 61, 82, JADE)
	_rect(image, 7, 8, 55, 76, GOLD_LIGHT if selected else IVORY_SHADOW)
	_rect(image, 9, 9, 51, 72, IVORY)
	# Warm, handmade paper variation.
	for y in range(12, 78, 5):
		for x in range(12 + (y % 3), 57, 7):
			if ((x * 17 + y * 31 + kind * 13) % 11) < 3:
				image.set_pixel(x, y, Color("dfd2ae"))
	# Double border, ornamental corners and jade rivets.
	_outline_rect(image, 7, 7, 56, 77, GOLD_LIGHT if selected else GOLD)
	_outline_rect(image, 10, 10, 50, 70, Color("82672b"))
	_corner(image, 12, 13, false, false)
	_corner(image, 56, 13, true, false)
	_corner(image, 12, 75, false, true)
	_corner(image, 56, 75, true, true)
	_face(image, kind)
	# Bottom royal seal.
	_diamond(image, 34, 78, 5, RED)
	_rect(image, 33, 75, 3, 7, Color("f0b64e"))
	if not free:
		# Locked pieces stay readable so the player can plan several layers
		# ahead; blend into existing pixels instead of replacing the face with
		# a translucent rectangle, which made most of the board disappear.
		_shade_rect(image, 4, 5, 61, 82, 0.72)
		for y in range(10, 84, 6):
			for x in range(8 + (y % 2) * 3, 64, 8):
				var pixel := image.get_pixel(x, y)
				image.set_pixel(x, y, Color(pixel.r * 0.72, pixel.g * 0.82, pixel.b * 0.78, pixel.a))
	return image


func _face(image: Image, kind: int) -> void:
	if kind < 9:
		_draw_circles(image, kind + 1)
	elif kind < 18:
		_draw_bamboo(image, kind - 8)
	elif kind < 27:
		_draw_character(image, kind - 17)
	elif kind < 31:
		var winds := ["E", "S", "W", "N"]
		_draw_medallion(image, winds[kind - 27], BLUE)
	elif kind == 31:
		_draw_dragon(image, "R", RED)
	elif kind == 32:
		_draw_dragon(image, "G", JADE)
	elif kind == 33:
		_draw_white_dragon(image)
	elif kind < 38:
		_draw_flower(image, kind - 34)
	else:
		_draw_season(image, kind - 38)


func _draw_circles(image: Image, count: int) -> void:
	var centers: Array[Vector2i] = []
	match count:
		1: centers = [Vector2i(34, 43)]
		2: centers = [Vector2i(34, 28), Vector2i(34, 57)]
		3: centers = [Vector2i(24, 27), Vector2i(34, 43), Vector2i(44, 59)]
		4: centers = [Vector2i(24, 29), Vector2i(44, 29), Vector2i(24, 57), Vector2i(44, 57)]
		5: centers = [Vector2i(23, 27), Vector2i(45, 27), Vector2i(34, 43), Vector2i(23, 59), Vector2i(45, 59)]
		6: centers = [Vector2i(24, 25), Vector2i(44, 25), Vector2i(24, 43), Vector2i(44, 43), Vector2i(24, 61), Vector2i(44, 61)]
		7: centers = [Vector2i(23, 24), Vector2i(45, 24), Vector2i(34, 34), Vector2i(23, 45), Vector2i(45, 45), Vector2i(23, 62), Vector2i(45, 62)]
		8: centers = [Vector2i(23, 23), Vector2i(45, 23), Vector2i(23, 36), Vector2i(45, 36), Vector2i(23, 50), Vector2i(45, 50), Vector2i(23, 63), Vector2i(45, 63)]
		9: centers = [Vector2i(22, 23), Vector2i(34, 23), Vector2i(46, 23), Vector2i(22, 43), Vector2i(34, 43), Vector2i(46, 43), Vector2i(22, 63), Vector2i(34, 63), Vector2i(46, 63)]
	var radius := 9 if count == 1 else (5 if count <= 5 else 4)
	for i in centers.size():
		var color: Color = [JADE, RED, BLUE][i % 3]
		_circle(image, centers[i].x, centers[i].y, radius + 1, GOLD)
		_circle(image, centers[i].x, centers[i].y, radius, color)
		_circle(image, centers[i].x, centers[i].y, maxi(1, radius - 2), IVORY)
		_circle(image, centers[i].x, centers[i].y, maxi(1, radius - 4), color)


func _draw_bamboo(image: Image, count: int) -> void:
	var columns := mini(3, count)
	var rows := ceili(float(count) / float(columns))
	var n := 0
	for row in rows:
		for col in columns:
			if n >= count:
				break
			var x := 22 + col * 12 + (row % 2) * 2
			var y := 25 + row * 18
			_line(image, x, y, x - 2, y + 12, JADE_DARK, 3)
			_line(image, x + 1, y, x - 1, y + 12, JADE_LIGHT, 1)
			_line(image, x - 4, y + 5, x + 4, y + 5, GOLD, 1)
			_line(image, x, y + 2, x + 6, y - 2, RED if n % 3 == 0 else JADE, 2)
			n += 1
	if count == 1:
		# The one-bamboo tile is a royal peacock.
		_circle(image, 34, 30, 7, BLUE)
		_circle(image, 34, 30, 4, GOLD_LIGHT)
		_line(image, 34, 36, 30, 62, JADE, 4)
		_line(image, 34, 42, 44, 55, BLUE, 3)
		_line(image, 32, 43, 22, 56, RED, 3)


func _draw_character(image: Image, number: int) -> void:
	_draw_pixel_glyph(image, str(number), 28, 17, 3, RED)
	# A detailed imperial coin and seal distinguish this suit.
	_outline_rect(image, 20, 43, 29, 23, JADE_DARK)
	_outline_rect(image, 23, 46, 23, 17, GOLD)
	_line(image, 27, 50, 42, 50, JADE, 2)
	_line(image, 27, 56, 42, 56, JADE, 2)
	_line(image, 30, 47, 30, 62, BLUE, 2)
	_line(image, 39, 47, 39, 62, BLUE, 2)


func _draw_medallion(image: Image, letter: String, color: Color) -> void:
	_circle(image, 34, 42, 23, GOLD)
	_circle(image, 34, 42, 20, JADE_DARK)
	_circle(image, 34, 42, 16, IVORY)
	_diamond(image, 34, 42, 13, Color("d5c48f"))
	_draw_pixel_glyph(image, letter, 27, 32, 3, color)


func _draw_dragon(image: Image, letter: String, color: Color) -> void:
	# Coiled dragon silhouette around a lacquer seal.
	_circle_outline(image, 34, 42, 22, GOLD, 3)
	_line(image, 18, 34, 25, 24, color, 4)
	_line(image, 25, 24, 43, 27, color, 4)
	_line(image, 43, 27, 50, 42, color, 4)
	_line(image, 50, 42, 42, 59, color, 4)
	_line(image, 42, 59, 22, 56, color, 4)
	_diamond(image, 34, 42, 14, IVORY)
	_draw_pixel_glyph(image, letter, 28, 33, 3, color)
	_circle(image, 24, 26, 2, GOLD_LIGHT)


func _draw_white_dragon(image: Image) -> void:
	_outline_rect(image, 18, 23, 34, 39, BLUE)
	_outline_rect(image, 21, 26, 28, 33, GOLD)
	for i in 4:
		_line(image, 24 + i * 7, 29, 24 + i * 7, 56, JADE, 1)
	for i in 4:
		_line(image, 24, 30 + i * 8, 46, 30 + i * 8, RED if i == 2 else BLUE, 1)


func _draw_flower(image: Image, variant: int) -> void:
	var palette := [RED, BLUE, JADE, Color("8a4f88")]
	var petal: Color = palette[variant]
	_line(image, 34, 38, 34, 68, JADE_DARK, 3)
	_line(image, 34, 51, 23, 57, JADE, 3)
	_line(image, 34, 55, 46, 50, JADE, 3)
	for angle_index in 8:
		var angle := float(angle_index) * TAU / 8.0
		var px := 34 + roundi(cos(angle) * 13.0)
		var py := 32 + roundi(sin(angle) * 13.0)
		_circle(image, px, py, 5, GOLD)
		_circle(image, px, py, 3, petal)
	_circle(image, 34, 32, 5, GOLD_LIGHT)
	_draw_pixel_glyph(image, str(variant + 1), 31, 64, 1, RED)


func _draw_season(image: Image, variant: int) -> void:
	var colors := [Color("5d9d55"), Color("d9b845"), Color("c45d32"), Color("5b79a0")]
	var color: Color = colors[variant]
	# Mountain, moon and falling leaves/snow in a miniature landscape.
	_circle(image, 46, 27, 8, GOLD_LIGHT)
	_line(image, 17, 59, 30, 34, JADE_DARK, 3)
	_line(image, 30, 34, 39, 52, color, 4)
	_line(image, 39, 52, 48, 41, BLUE, 3)
	_line(image, 48, 41, 55, 60, JADE_DARK, 3)
	_line(image, 17, 60, 55, 60, GOLD, 2)
	for i in 7:
		var x := 17 + ((i * 13 + variant * 5) % 38)
		var y := 20 + ((i * 17 + variant * 11) % 37)
		_diamond(image, x, y, 2, color)
	_draw_pixel_glyph(image, str(variant + 1), 31, 64, 1, RED)


func _corner(image: Image, x: int, y: int, flip_x: bool, flip_y: bool) -> void:
	var sx := -1 if flip_x else 1
	var sy := -1 if flip_y else 1
	_line(image, x, y, x + sx * 7, y, GOLD, 2)
	_line(image, x, y, x, y + sy * 7, GOLD, 2)
	_diamond(image, x + sx * 3, y + sy * 3, 2, JADE)


func _rect(image: Image, x: int, y: int, w: int, h: int, color: Color) -> void:
	image.fill_rect(Rect2i(x, y, w, h), color)


func _outline_rect(image: Image, x: int, y: int, w: int, h: int, color: Color) -> void:
	_rect(image, x, y, w, 2, color)
	_rect(image, x, y + h - 2, w, 2, color)
	_rect(image, x, y, 2, h, color)
	_rect(image, x + w - 2, y, 2, h, color)


func _shade_rect(image: Image, x: int, y: int, w: int, h: int, factor: float) -> void:
	for py in range(y, y + h):
		for px in range(x, x + w):
			var pixel := image.get_pixel(px, py)
			if pixel.a > 0.0:
				image.set_pixel(px, py, Color(pixel.r * factor, pixel.g * factor, pixel.b * factor, pixel.a))


func _line(image: Image, x0: int, y0: int, x1: int, y1: int, color: Color, thickness := 1) -> void:
	var dx := absi(x1 - x0)
	var sx := 1 if x0 < x1 else -1
	var dy := -absi(y1 - y0)
	var sy := 1 if y0 < y1 else -1
	var error := dx + dy
	while true:
		for oy in thickness:
			for ox in thickness:
				_safe_pixel(image, x0 + ox - thickness / 2, y0 + oy - thickness / 2, color)
		if x0 == x1 and y0 == y1:
			break
		var doubled := 2 * error
		if doubled >= dy:
			error += dy
			x0 += sx
		if doubled <= dx:
			error += dx
			y0 += sy


func _circle(image: Image, cx: int, cy: int, radius: int, color: Color) -> void:
	for y in range(-radius, radius + 1):
		for x in range(-radius, radius + 1):
			if x * x + y * y <= radius * radius:
				_safe_pixel(image, cx + x, cy + y, color)


func _circle_outline(image: Image, cx: int, cy: int, radius: int, color: Color, thickness: int) -> void:
	var inner := (radius - thickness) * (radius - thickness)
	var outer := radius * radius
	for y in range(-radius, radius + 1):
		for x in range(-radius, radius + 1):
			var distance := x * x + y * y
			if distance <= outer and distance >= inner:
				_safe_pixel(image, cx + x, cy + y, color)


func _diamond(image: Image, cx: int, cy: int, radius: int, color: Color) -> void:
	for y in range(-radius, radius + 1):
		for x in range(-radius, radius + 1):
			if absi(x) + absi(y) <= radius:
				_safe_pixel(image, cx + x, cy + y, color)


func _draw_pixel_glyph(image: Image, glyph: String, x: int, y: int, scale: int, color: Color) -> void:
	var patterns := {
		"1": ["010", "110", "010", "010", "111"],
		"2": ["110", "001", "010", "100", "111"],
		"3": ["110", "001", "010", "001", "110"],
		"4": ["101", "101", "111", "001", "001"],
		"5": ["111", "100", "110", "001", "110"],
		"6": ["011", "100", "111", "101", "111"],
		"7": ["111", "001", "010", "010", "010"],
		"8": ["111", "101", "111", "101", "111"],
		"9": ["111", "101", "111", "001", "110"],
		"E": ["111", "100", "110", "100", "111"],
		"S": ["111", "100", "111", "001", "111"],
		"W": ["101", "101", "101", "111", "101"],
		"N": ["101", "111", "111", "111", "101"],
		"R": ["110", "101", "110", "101", "101"],
		"G": ["111", "100", "101", "101", "111"],
	}
	var pattern: Array = patterns.get(glyph, patterns["1"])
	for row in pattern.size():
		for col in pattern[row].length():
			if pattern[row][col] == "1":
				_rect(image, x + col * scale, y + row * scale, scale, scale, color)


func _safe_pixel(image: Image, x: int, y: int, color: Color) -> void:
	if x >= 0 and x < WIDTH and y >= 0 and y < HEIGHT:
		image.set_pixel(x, y, color)
