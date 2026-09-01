class_name BoardLayout
extends RefCounted

## Geometry shared by rendering, touch hit-testing and regression tests.
## Keeping it in one place prevents the visual board and the tappable board
## from drifting apart when Android scales the viewport.

const TILE_ASPECT := 0.75
const STEP_X_FACTOR := 0.91
const STEP_Y_FACTOR := 0.82
const LAYER_SHIFT_FACTOR := 0.085
const OUTER_PADDING := Vector2(26.0, 22.0)

var rects: Dictionary = {}
var draw_order: Array[int] = []
var tile_size := Vector2.ZERO
var step := Vector2.ZERO
var layer_shift := 0.0
var board_bounds := Rect2()


func rebuild(view_size: Vector2, tiles: Array[Dictionary]) -> void:
	rects.clear()
	draw_order.clear()
	if tiles.is_empty() or view_size.x <= 0.0 or view_size.y <= 0.0:
		tile_size = Vector2.ZERO
		board_bounds = Rect2()
		return

	var min_gx := 1 << 30
	var max_gx := -(1 << 30)
	var min_gy := 1 << 30
	var max_gy := -(1 << 30)
	var max_layer := 0
	for tile in tiles:
		min_gx = mini(min_gx, int(tile.gx))
		max_gx = maxi(max_gx, int(tile.gx))
		min_gy = mini(min_gy, int(tile.gy))
		max_gy = maxi(max_gy, int(tile.gy))
		max_layer = maxi(max_layer, int(tile.layer))
		if tile.active:
			draw_order.append(int(tile.id))

	draw_order.sort_custom(func(a: int, b: int) -> bool:
		var first: Dictionary = tiles[a]
		var second: Dictionary = tiles[b]
		if first.layer != second.layer:
			return first.layer < second.layer
		if first.gy != second.gy:
			return first.gy < second.gy
		if first.gx != second.gx:
			return first.gx < second.gx
		return first.id < second.id
	)

	var horizontal_intervals := float(max_gx - min_gx) * 0.5
	var vertical_intervals := float(max_gy - min_gy) * 0.5
	var width_units := horizontal_intervals * STEP_X_FACTOR + 1.0 + float(max_layer) * LAYER_SHIFT_FACTOR
	var height_units := (vertical_intervals * STEP_Y_FACTOR + 1.0) / TILE_ASPECT + float(max_layer) * LAYER_SHIFT_FACTOR
	var usable := Vector2(maxf(1.0, view_size.x - OUTER_PADDING.x * 2.0), maxf(1.0, view_size.y - OUTER_PADDING.y * 2.0))
	var tile_width := minf(usable.x / width_units, usable.y / height_units)
	tile_width = clampf(tile_width, 28.0, 96.0)
	tile_size = Vector2(tile_width, tile_width / TILE_ASPECT)
	step = Vector2(tile_width * STEP_X_FACTOR, tile_size.y * STEP_Y_FACTOR)
	layer_shift = tile_width * LAYER_SHIFT_FACTOR

	var layout_size := Vector2(
		horizontal_intervals * step.x + tile_size.x + float(max_layer) * layer_shift,
		vertical_intervals * step.y + tile_size.y + float(max_layer) * layer_shift
	)
	var top_left := (view_size - layout_size) * 0.5
	var base_origin := top_left + Vector2(0.0, float(max_layer) * layer_shift)
	board_bounds = Rect2(top_left, layout_size)

	for tile_id in draw_order:
		var tile: Dictionary = tiles[tile_id]
		var px := base_origin.x + float(tile.gx - min_gx) * 0.5 * step.x + float(tile.layer) * layer_shift
		var py := base_origin.y + float(tile.gy - min_gy) * 0.5 * step.y - float(tile.layer) * layer_shift
		rects[tile_id] = Rect2(Vector2(px, py), tile_size)


func pick_tile(point: Vector2, game) -> int:
	# Draw order is also hit order. Prefer a free tile when two shallow
	# perspective layers overlap, which makes the exposed part forgiving on
	# a touchscreen without allowing removal of a logically blocked tile.
	var topmost := -1
	for index in range(draw_order.size() - 1, -1, -1):
		var tile_id: int = draw_order[index]
		var rect: Rect2 = rects[tile_id]
		if not rect.has_point(point):
			continue
		if topmost < 0:
			topmost = tile_id
		if game.is_free(tile_id):
			return tile_id
	if topmost >= 0:
		return topmost

	# A small accessibility slop helps fingers near an outside edge. It is
	# only applied to free pieces and selects the nearest centre.
	var nearest := -1
	var nearest_distance := INF
	for tile_id in draw_order:
		if not game.is_free(tile_id):
			continue
		var rect: Rect2 = rects[tile_id]
		if rect.grow(7.0).has_point(point):
			var distance := point.distance_squared_to(rect.get_center())
			if distance < nearest_distance:
				nearest = tile_id
				nearest_distance = distance
	return nearest
