package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Material;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.BlendingAttribute;

public class BlendAnimation {

	private BlendingAttribute attribute;
	private float transitionCurrentTime, transitionTargetTime;
	private float opacityStep;
	private float sig;
	private boolean inAction;

	public BlendAnimation(ModelInstance model, float duration) {
		Material material = model.materials.first();
		attribute = (BlendingAttribute) material.get(BlendingAttribute.Type);
		// Set animation to 30 frames per second and start it.
		transitionCurrentTime = 0.0f;
		transitionTargetTime = duration/30.0f;
		opacityStep = 1.0f/30.0f;
		inAction = true;
	}

	/*
	 * Update the animation based on the times passed since last frame.
	 * If delta is negative, the animation plays backwards (to opaque).
	 */
	public void update(float delta) {
		if (!inAction)
			return;

		// Update opacity if target time has been reached.
		sig = Math.signum(delta);
		transitionCurrentTime += Math.abs(delta);
		if (transitionCurrentTime >= transitionTargetTime) {
			if (sig == 1.0f)
				attribute.opacity -= opacityStep;
			else
				attribute.opacity += opacityStep;

			transitionCurrentTime -= transitionTargetTime;

			// Stop the animation if it has reached the end.
			if ((sig == 1.0f && attribute.opacity <= 0.0f)
					|| (sig == -1.0f && attribute.opacity >= 1.0f)) {
				transitionCurrentTime = 0.0f;
				inAction = false;
			}
		}
	}

	/*
	 * Reset the animation to the chosen opacity.
	 */
	public void reset(float opacity) {
		attribute.opacity = opacity;
		transitionCurrentTime = 0.0f;
		inAction = true;
	}

	public boolean isInAction() {
		return inAction;
	}

}

