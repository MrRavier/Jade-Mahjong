class_name JadeAudio
extends Node

const RATE := 22050

var music_enabled := true
var music: AudioStreamPlayer
var effects: AudioStreamPlayer


func _ready() -> void:
	music = AudioStreamPlayer.new()
	effects = AudioStreamPlayer.new()
	music.name = "CorteDeJade"
	effects.name = "Efeitos"
	music.volume_db = -15.0
	effects.volume_db = -7.0
	add_child(music)
	add_child(effects)
	music.stream = _make_theme()
	music.play()


func toggle_music() -> bool:
	music_enabled = not music_enabled
	if music_enabled:
		music.play()
	else:
		music.stop()
	return music_enabled


func play_pair() -> void:
	effects.stream = _make_chime([659.25, 783.99, 987.77], 0.11)
	effects.play()


func play_error() -> void:
	effects.stream = _make_chime([196.0, 174.61], 0.16, true)
	effects.play()


func play_gong() -> void:
	effects.stream = _make_gong()
	effects.play()


func play_victory() -> void:
	effects.stream = _make_chime([392.0, 523.25, 659.25, 783.99, 1046.5], 0.18)
	effects.play()


func _make_theme() -> AudioStreamWAV:
	# Original pentatonic court theme: "Corte de Jade".
	var notes := [
		392.0, 440.0, 523.25, 659.25, 523.25, 440.0, 392.0, 329.63,
		392.0, 523.25, 587.33, 659.25, 783.99, 659.25, 523.25, 440.0,
		329.63, 392.0, 440.0, 523.25, 440.0, 392.0, 329.63, 293.66,
		329.63, 392.0, 523.25, 587.33, 523.25, 440.0, 392.0, 0.0,
	]
	var beat := 0.30
	var sample_count := int(notes.size() * beat * RATE)
	var bytes := PackedByteArray()
	bytes.resize(sample_count * 2)
	for sample in sample_count:
		var time := float(sample) / RATE
		var note_index := mini(notes.size() - 1, int(time / beat))
		var local_time := fmod(time, beat)
		var frequency: float = notes[note_index]
		var envelope := minf(1.0, local_time / 0.015) * exp(-local_time * 2.8)
		var value := 0.0
		if frequency > 0.0:
			var pluck := sin(TAU * frequency * time) * 0.52
			pluck += sin(TAU * frequency * 2.01 * time) * 0.20
			pluck += sin(TAU * frequency * 3.98 * time) * 0.07
			var drone := sin(TAU * (98.0 if note_index % 8 < 4 else 130.81) * time) * 0.11
			value = (pluck * envelope + drone) * 0.55
		_write_sample(bytes, sample, value)
	var stream := _stream_from_bytes(bytes)
	stream.loop_mode = AudioStreamWAV.LOOP_FORWARD
	stream.loop_begin = 0
	stream.loop_end = sample_count
	return stream


func _make_chime(notes: Array, duration: float, rough := false) -> AudioStreamWAV:
	var sample_count := int(notes.size() * duration * RATE)
	var bytes := PackedByteArray()
	bytes.resize(sample_count * 2)
	for sample in sample_count:
		var time := float(sample) / RATE
		var note_index := mini(notes.size() - 1, int(time / duration))
		var local_time := fmod(time, duration)
		var frequency: float = notes[note_index]
		var envelope := exp(-local_time * (7.0 if rough else 4.0))
		var wave := sin(TAU * frequency * time)
		if rough:
			wave = 1.0 if wave >= 0.0 else -1.0
		var value := wave * envelope * 0.48 + sin(TAU * frequency * 2.0 * time) * envelope * 0.12
		_write_sample(bytes, sample, value)
	return _stream_from_bytes(bytes)


func _make_gong() -> AudioStreamWAV:
	var duration := 1.6
	var sample_count := int(duration * RATE)
	var bytes := PackedByteArray()
	bytes.resize(sample_count * 2)
	for sample in sample_count:
		var time := float(sample) / RATE
		var envelope := exp(-time * 2.15)
		var value := sin(TAU * 110.0 * time) * 0.35
		value += sin(TAU * 171.0 * time) * 0.22
		value += sin(TAU * 278.0 * time) * 0.12
		value += sin(TAU * 421.0 * time) * 0.06
		_write_sample(bytes, sample, value * envelope)
	return _stream_from_bytes(bytes)


func _stream_from_bytes(bytes: PackedByteArray) -> AudioStreamWAV:
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = RATE
	stream.stereo = false
	stream.data = bytes
	return stream


func _write_sample(bytes: PackedByteArray, sample_index: int, value: float) -> void:
	var integer := int(clampf(value, -1.0, 1.0) * 32767.0)
	if integer < 0:
		integer += 65536
	bytes[sample_index * 2] = integer & 255
	bytes[sample_index * 2 + 1] = (integer >> 8) & 255
