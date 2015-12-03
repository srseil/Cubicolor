package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Material;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.BlendingAttribute;
import com.badlogic.gdx.math.MathUtils;

public class BlendAnimation {

	private Material material;
	private BlendingAttribute attribute;
	private float transitionCurrentTime, transitionTargetTime;
	private boolean inAction;
	private float sig;

	public BlendAnimation(ModelInstance model, float duration) {
		material = model.materials.first();
		attribute = (BlendingAttribute) material.get(BlendingAttribute.Type);
		transitionCurrentTime = 0.0f;
		transitionTargetTime = duration/20.0f;
		inAction = true;
	}

	public void update(float delta) {
		if (!inAction)
			return;

		sig = Math.signum(delta);

		if ((sig == 1.0f && attribute.opacity <= 0.0f) ||
				(sig == -1.0f && attribute.opacity >= 1.0f)) {
			inAction = false;
			transitionCurrentTime = 0.0f;
			return;
		}

		transitionCurrentTime += Math.abs(delta);
		if (transitionCurrentTime >= transitionTargetTime) {
			if (sig == 1.0f)
				attribute.opacity -= 0.05f;
			else
				attribute.opacity += 0.05f;
			transitionCurrentTime -= transitionTargetTime;
		}
	}

	// Manually reset animation.
	public void reset(float opacity) {
		attribute.opacity = opacity;
		transitionCurrentTime = 0.0f;
		inAction = true;
	}

	public boolean isInAction() {
		return inAction;
	}

}
