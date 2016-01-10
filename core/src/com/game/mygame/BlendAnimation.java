package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Material;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.BlendingAttribute;

public class BlendAnimation {

	private BlendingAttribute attribute;
	private float duration;
	private float transitionCurrentTime, transitionTargetTime;
	private float opacityStep;
	private float sig;
	private boolean inAction;

	private float a;

	public BlendAnimation(ModelInstance model, float duration, float speed) {
		this.duration = duration;
		Material material = model.materials.first();
		attribute = (BlendingAttribute) material.get(BlendingAttribute.Type);
		attribute.blended = true;
		// Set animation to 30 frames per second and start it.
		transitionCurrentTime = 0.0f;
		transitionTargetTime = duration / 30.0f / speed;
		opacityStep = 1.0f / 30.0f;
		inAction = true;

		a = 0;
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
		while (transitionCurrentTime >= transitionTargetTime) {
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
				break;
			}
		}
	}

	// For debugging purposes to check how long animations take.
	public void update(float delta, boolean b) {
		if (!inAction)
			return;

		// Update opacity if target time has been reached.
		sig = Math.signum(delta);
		transitionCurrentTime += Math.abs(delta);

		a += Math.abs(delta);

		while (transitionCurrentTime >= transitionTargetTime) {
			System.out.println("current: "  + transitionCurrentTime + ", target: " + transitionTargetTime);

			if (sig == 1.0f)
				attribute.opacity -= opacityStep;
			else
				attribute.opacity += opacityStep;

			/*
			System.out.println("BLENDANIMATION: " + attribute.opacity);
			*/

			transitionCurrentTime -= transitionTargetTime;

			// Stop the animation if it has reached the end.
			if ((sig == 1.0f && attribute.opacity <= 0.0f)
					|| (sig == -1.0f && attribute.opacity >= 1.0f)) {
				System.out.println("It took " + a);
				transitionCurrentTime = 0.0f;
				inAction = false;
				break;
			}
		}
	}

	/*
	 * Reset the animation to the chosen opacity.
	 */
	public void reset(float opacity, float speed) {
		attribute.opacity = opacity;
		transitionTargetTime = duration / 30.0f / speed;
		transitionCurrentTime = 0.0f;
		inAction = true;
	}

	/*
	 * Specify if the model of the animation is treated as blended.
	 */
	public void setBlended(boolean blended) {
		attribute.blended = blended;
	}

	public boolean isInAction() {
		return inAction;
	}

}

