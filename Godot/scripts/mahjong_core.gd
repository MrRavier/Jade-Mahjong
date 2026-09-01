class_name MahjongCore
extends RefCounted

const TOTAL_TILES := 144
const FLOWER_FIRST := 34
const SEASON_FIRST := 38

var seed: int = 1
var tiles: Array[Dictionary] = []
var solution: Array[Vector2i] = []
var remaining: int = 0
var _rng := JadeRandom.new()


func build_board(p_seed: int) -> void:
	seed = p_seed if p_seed != 0 else 1
	_rng.set_seed(seed)
	tiles.clear()
	solution.clear()
	_build_slots()
	var removal_pairs := _calculate_removal_pairs()
	assert(removal_pairs.size() == 72, "The palace layout must yield 72 legal pairs")
	var kind_pairs := _make_kind_pairs()
	_rng.shuffle(kind_pairs)
	for i in removal_pairs.size():
		var positions: Vector2i = removal_pairs[i]
		var kinds: Vector2i = kind_pairs[i]
		tiles[positions.x].kind = kinds.x
		tiles[positions.y].kind = kinds.y
		solution.append(positions)
	for tile in tiles:
		tile.active = true
	remaining = TOTAL_TILES


func is_free(tile_id: int) -> bool:
	if tile_id < 0 or tile_id >= tiles.size() or not tiles[tile_id].active:
		return false
	return _is_free_with_state(tile_id)


func matches(a_id: int, b_id: int) -> bool:
	if a_id == b_id or a_id < 0 or b_id < 0:
		return false
	var a_kind: int = tiles[a_id].kind
	var b_kind: int = tiles[b_id].kind
	if a_kind == b_kind:
		return true
	return _family(a_kind) >= 2 and _family(a_kind) == _family(b_kind)


func try_remove(a_id: int, b_id: int) -> bool:
	if not is_free(a_id) or not is_free(b_id) or not matches(a_id, b_id):
		return false
	tiles[a_id].active = false
	tiles[b_id].active = false
	remaining -= 2
	return true


func available_pair() -> Vector2i:
	var free_ids: Array[int] = []
	for i in tiles.size():
		if is_free(i):
			free_ids.append(i)
	for a in free_ids.size():
		for b in range(a + 1, free_ids.size()):
			if matches(free_ids[a], free_ids[b]):
				return Vector2i(free_ids[a], free_ids[b])
	return Vector2i(-1, -1)


func has_available_pair() -> bool:
	return available_pair().x >= 0


func redeal_remaining() -> bool:
	var active_ids: Array[int] = []
	var kinds: Array[int] = []
	for i in tiles.size():
		if tiles[i].active:
			active_ids.append(i)
			kinds.append(tiles[i].kind)
	if active_ids.size() < 2:
		return true
	for attempt in 80:
		_rng.shuffle(kinds)
		for j in active_ids.size():
			tiles[active_ids[j]].kind = kinds[j]
		if has_available_pair():
			return true
	return false


func active_tile_ids() -> Array[int]:
	var result: Array[int] = []
	for i in tiles.size():
		if tiles[i].active:
			result.append(i)
	return result


func layout_signature() -> String:
	var parts: PackedStringArray = []
	for tile in tiles:
		parts.append("%d:%d:%d:%d" % [tile.gx, tile.gy, tile.layer, tile.kind])
	return "|".join(parts)


func _build_slots() -> void:
	# A centred five-level palace pyramid. The former layout packed eight
	# heavily overlapping rows into the mobile board, which made individual
	# pieces hard to read and almost impossible to tap. This keeps all 144
	# pieces while using six clean base rows and a legible stepped silhouette.
	for row in 6:
		for col in 12:
			_add_slot(col * 2, row * 2, 0)
	for row in 4:
		for col in 10:
			_add_slot(2 + col * 2, 2 + row * 2, 1)
	for row in 4:
		for col in 6:
			_add_slot(6 + col * 2, 2 + row * 2, 2)
	for row in 2:
		for col in 3:
			_add_slot(9 + col * 2, 4 + row * 2, 3)
	for gx in [10, 12]:
		_add_slot(gx, 5, 4)
	assert(tiles.size() == TOTAL_TILES)


func _add_slot(gx: int, gy: int, layer: int) -> void:
	tiles.append({
		"id": tiles.size(),
		"gx": gx,
		"gy": gy,
		"layer": layer,
		"kind": 0,
		"active": true,
	})


func _calculate_removal_pairs() -> Array[Vector2i]:
	var pairs: Array[Vector2i] = []
	for _step in 72:
		var free_ids: Array[int] = []
		for i in tiles.size():
			if _is_free_with_state(i):
				free_ids.append(i)
		if free_ids.size() < 2:
			break
		free_ids.sort_custom(func(a: int, b: int) -> bool:
			var ta: Dictionary = tiles[a]
			var tb: Dictionary = tiles[b]
			if ta.layer != tb.layer:
				return ta.layer > tb.layer
			var da: float = absf(float(ta.gx) - 11.0) + absf(float(ta.gy) - 5.0)
			var db: float = absf(float(tb.gx) - 11.0) + absf(float(tb.gy) - 5.0)
			return da > db
		)
		var first: int = free_ids[0]
		var second: int = free_ids[1]
		# Favor opposite sides to keep the reveal visually balanced.
		for candidate in free_ids:
			if tiles[candidate].layer == tiles[first].layer and \
					signf(float(tiles[candidate].gx) - 11.0) != signf(float(tiles[first].gx) - 11.0):
				second = candidate
				break
		tiles[first].active = false
		tiles[second].active = false
		pairs.append(Vector2i(first, second))
	return pairs


func _make_kind_pairs() -> Array[Vector2i]:
	var result: Array[Vector2i] = []
	for kind in 34:
		result.append(Vector2i(kind, kind))
		result.append(Vector2i(kind, kind))
	result.append(Vector2i(34, 35))
	result.append(Vector2i(36, 37))
	result.append(Vector2i(38, 39))
	result.append(Vector2i(40, 41))
	assert(result.size() == 72)
	return result


func _family(kind: int) -> int:
	if kind >= SEASON_FIRST:
		return 3
	if kind >= FLOWER_FIRST:
		return 2
	return kind


func _is_free_with_state(tile_id: int) -> bool:
	var tile: Dictionary = tiles[tile_id]
	if not tile.active:
		return false
	var left_blocked := false
	var right_blocked := false
	for other in tiles:
		if not other.active or other.id == tile_id:
			continue
		if other.layer > tile.layer and abs(other.gx - tile.gx) < 2 and abs(other.gy - tile.gy) < 2:
			return false
		if other.layer == tile.layer and abs(other.gy - tile.gy) < 2:
			var dx: int = other.gx - tile.gx
			if dx > 0 and dx <= 2:
				right_blocked = true
			elif dx < 0 and dx >= -2:
				left_blocked = true
	return not (left_blocked and right_blocked)


class JadeRandom:
	var _state: int = 1

	func set_seed(value: int) -> void:
		_state = value & 0x7fffffff
		if _state == 0:
			_state = 0x13579bdf

	func next_int(max_exclusive: int) -> int:
		_state ^= (_state << 13) & 0x7fffffff
		_state ^= _state >> 17
		_state ^= (_state << 5) & 0x7fffffff
		_state &= 0x7fffffff
		return _state % max_exclusive

	func shuffle(values: Array) -> void:
		for i in range(values.size() - 1, 0, -1):
			var j := next_int(i + 1)
			var temp = values[i]
			values[i] = values[j]
			values[j] = temp
