"""Bake-pass contracts, image padding, and deterministic channel mixing."""

import numpy as np


BAKE_PASSES = {
    "AO": {"color_space": "Non-Color", "rgb": False},
    "DIFFUSE": {"color_space": "sRGB", "rgb": True},
    "OPACITY": {"color_space": "Non-Color", "rgb": False},
    "NORMAL": {"color_space": "Non-Color", "rgb": True, "background": (0.5, 0.5, 1.0)},
    "ROOT": {"color_space": "Non-Color", "rgb": False},
    "ID": {"color_space": "Non-Color", "rgb": False},
    "RANDOM_COLOR": {"color_space": "sRGB", "rgb": True},
    "CLUMP_COLOR": {"color_space": "sRGB", "rgb": True},
    "FLOW": {"color_space": "Non-Color", "rgb": True, "background": (0.5, 0.5, 0.0)},
    "DIRECTIONAL": {"color_space": "Non-Color", "rgb": True},
    "DEPTH": {"color_space": "Non-Color", "rgb": False},
    "COLOR": {"color_space": "sRGB", "rgb": True},
}


def pass_settings(name):
    if name not in BAKE_PASSES:
        raise ValueError(f"unknown bake pass {name!r}")
    return dict(BAKE_PASSES[name])


def _image(image):
    image = np.asarray(image, dtype=np.float64)
    if image.ndim != 3 or image.shape[2] != 4 or not np.isfinite(image).all():
        raise ValueError("image must be a finite HxWx4 RGBA array")
    return image


def pad_image(image, padding, background=None):
    image = _image(image)
    if int(padding) != padding or padding < 0:
        raise ValueError("padding must be a nonnegative integer")
    padding = int(padding)
    if background is None:
        return np.pad(image, ((padding, padding), (padding, padding), (0, 0)), mode="edge")
    background = np.asarray(background, dtype=np.float64)
    if background.shape != (4,) or not np.isfinite(background).all():
        raise ValueError("background must contain four finite RGBA values")
    shape = (image.shape[0] + 2 * padding, image.shape[1] + 2 * padding, 4)
    output = np.broadcast_to(background, shape).copy()
    output[padding:padding + image.shape[0], padding:padding + image.shape[1]] = image
    return output


def separate_rgb(image):
    image = _image(image)
    return {"red": image[:, :, 0].copy(), "green": image[:, :, 1].copy(),
            "blue": image[:, :, 2].copy()}


def combine_rgba(red, green, blue, alpha):
    channels = [np.asarray(channel, dtype=np.float64) for channel in (red, green, blue, alpha)]
    if any(channel.ndim != 2 or channel.shape != channels[0].shape or not np.isfinite(channel).all()
           for channel in channels):
        raise ValueError("RGBA channels must be finite, equally sized 2D arrays")
    return np.stack(channels, axis=2)


def set_alpha(image, alpha):
    image = _image(image)
    alpha = np.asarray(alpha, dtype=np.float64)
    if alpha.ndim == 0:
        alpha = np.full(image.shape[:2], float(alpha))
    if alpha.shape != image.shape[:2] or not np.isfinite(alpha).all():
        raise ValueError("alpha must be scalar or a finite 2D array matching the image")
    output = image.copy()
    output[:, :, 3] = alpha
    return output


def mix_rgba(first, second, factor):
    first, second = _image(first), _image(second)
    if first.shape != second.shape:
        raise ValueError("mixed images must have identical dimensions")
    if not 0 <= factor <= 1 or not np.isfinite(factor):
        raise ValueError("mix factor must be in [0, 1]")
    return first * (1.0 - factor) + second * factor


class TextureGraph:
    def __init__(self):
        self.nodes = {}
        self.execution_counts = {}
        self.execution_order = []

    def _add(self, name, operation, inputs=(), **settings):
        if not name or name in self.nodes:
            raise ValueError("node names must be nonempty and unique")
        self.nodes[name] = {"operation": operation, "inputs": tuple(inputs), "settings": settings}

    def add_input(self, name, image):
        self._add(name, "input", image=_image(image))

    def add_output(self, name, source):
        self._add(name, "output", (source,))

    def add_padding(self, name, source, padding, background=None):
        self._add(name, "padding", (source,), padding=padding, background=background)

    def add_mix(self, name, first, second, factor):
        self._add(name, "mix", (first, second), factor=factor)

    def add_set_alpha(self, name, source, alpha):
        inputs = (source, alpha) if isinstance(alpha, str) else (source,)
        self._add(name, "alpha", inputs, alpha=alpha)

    def execute(self):
        values = {}
        self.execution_counts = {name: 0 for name in self.nodes}
        self.execution_order = []
        visiting = set()

        def evaluate(name):
            if name in values:
                return values[name]
            if name not in self.nodes:
                raise ValueError(f"missing input node {name!r}")
            if name in visiting:
                raise ValueError(f"cycle detected at node {name!r}")
            visiting.add(name)
            node = self.nodes[name]
            operation = node["operation"]
            settings = node["settings"]
            inputs = [evaluate(source) for source in node["inputs"]]
            if operation == "input":
                value = settings["image"].copy()
            elif operation == "output":
                value = inputs[0].copy()
            elif operation == "padding":
                value = pad_image(inputs[0], settings["padding"], settings["background"])
            elif operation == "mix":
                factor = settings["factor"]
                value = mix_rgba(inputs[0], inputs[1], factor)
            elif operation == "alpha":
                alpha = inputs[1] if len(inputs) > 1 else settings["alpha"]
                value = set_alpha(inputs[0], alpha)
            else:
                raise ValueError(f"unknown node operation {operation!r}")
            visiting.remove(name)
            values[name] = value
            self.execution_counts[name] += 1
            self.execution_order.append(name)
            return value

        for name in self.nodes:
            evaluate(name)
        return values
