package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;

public class RequirementModel extends ModelInstance
		implements AnimationController.AnimationListener {

	// The speed the model's fall animation is played at.
	public static float FALL_SPEED = 2.0f;

	/*
	 * The possible states a RequirementModel can be in.
	 */
	private enum State {
		STILL,
		MOVING_DOWN,
		MOVING_UP,
		DESTROYING,
		REVIVING
	}

	private State state;
	private ExitTileModel exitModel;
	private AnimationController moveAnimation;
	private BlendAnimation blendAnimation;
	private TileColor color;
	private int height, defaultHeight;
	private float x, z;
	private boolean onHold;
	private boolean alive;

	public RequirementModel(TileColor color, int height, float x, float z,
							ExitTileModel exitModel, MyGame game) {
		super(game.getLockTileModel(color));
		this.color = color;
		this.exitModel = exitModel;
		this.height = height;
		this.x = x;
		this.z = z;
		defaultHeight = height;
		moveAnimation = new AnimationController(this);
		moveAnimation.allowSameAnimation = true;
		moveAnimation.setAnimation("Cube|Fall2");
		blendAnimation = new BlendAnimation(this, 0.6f, 1.0f);
		blendAnimation.reset(0.0f, 1.0f);
		transform.setTranslation(x, (height+1) * TileModel.SIZE/2, z);
		alive = false;
		state = State.STILL;
	}

	/*
	 * Update the models's position by the specified height.
	 */
	private void updateTransform(float deltaHeight) {
		transform.setTranslation(
				x, (height + deltaHeight + 1) * TileModel.SIZE/2, z);
	}

	@Override
	public void onEnd(AnimationController.AnimationDesc animation) {
		if (animation.animation.id.equals("Cube|Fall2")) {
			if (state == State.MOVING_DOWN) {
				// Model is moving down.
				state = State.STILL;
				moveAnimation.setAnimation("Cube|Fall2", 1, FALL_SPEED, this);
				height--;
				updateTransform(0);
			} else if (state == State.MOVING_UP) {
				// Model is moving up.
				height++;
				if (height == defaultHeight) {
					// Model has reached default height; stop moving.
					state = State.STILL;
					moveAnimation.setAnimation(
							"Cube|Fall2", 1, FALL_SPEED, this);
					updateTransform(0);
				} else {
					// Model still has to go up; keep moving.
					moveAnimation.setAnimation(
							"Cube|Fall2", 1, -FALL_SPEED, this);
					updateTransform(1.0f);
				}
			}
		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {}

	/*
	 * Update the animation of the model based on the time passed since the
	 * last frame.
	 */
	public void update(float delta) {
		if (onHold)
			return;

		switch (state) {
			case STILL:
				// Particles?
				break;
			case MOVING_DOWN: case MOVING_UP:
				moveAnimation.update(delta);
				break;
			case DESTROYING:
				blendAnimation.update(delta);
				if (!blendAnimation.isInAction()) {
					alive = false;
					state = State.STILL;
					exitModel.release();
				}
				break;
			case REVIVING:
				blendAnimation.update(-delta);
				if (!blendAnimation.isInAction()) {
					alive = true;
					state = State.STILL;
				}
		}
	}

	/*
	 * Let the model fall down to snap to remaining requirement models
	 */
	public void fall() {
		moveAnimation.setAnimation("Cube|Fall2", 1, FALL_SPEED, this);
		state = State.MOVING_DOWN;
	}

	/*
	 * Initiate the visual destruction of the model.
	 */
	public void destroy() {
		if (alive) {
			blendAnimation.reset(1.0f, 1.0f);
			state = State.DESTROYING;
		}
	}

	/*
	 * Initiate the visual revival of the model.
	 */
	public void revive() {
		if (!alive) {
			blendAnimation.reset(0.0f, 1.0f);
			state = State.REVIVING;
		}
	}

	/*
	 * Reset the model to its default state and position.
	 */
	public void reset() {
		if (height != defaultHeight) {
			moveAnimation.setAnimation("Cube|Fall2", 1, -FALL_SPEED, this);
			updateTransform(1.0f);
			state = State.MOVING_UP;
		}
	}

	/*
	 * Hold the model until it manually gets released.
	 */
	public void hold() {
		onHold = true;
	}

	/*
	 * Release the model if it is on hold.
	 */
	public void release() {
		onHold = false;
	}

	public TileColor getColor() {
		return color;
	}

	public boolean isAlive() {
		//return (state != State.DEAD);
		return alive;
	}

	public boolean isVisible() {
		return !(state == State.STILL && !alive);
	}

}

