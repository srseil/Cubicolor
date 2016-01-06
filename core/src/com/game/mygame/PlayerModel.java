package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.TextureAttribute;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;
import com.badlogic.gdx.math.Vector3;

import java.util.EnumMap;

public class PlayerModel extends ModelInstance
		implements Observer, AnimationController.AnimationListener {

	// The time in seconds before the end of an animation the player can be
	// controlled. This allows for smoother controls.
	public static final float CONTROL_MARGIN = 0.2f;
	// The speed the model's animation are being played at.
	public static final float MOVE_SPEED = 3.0f;
	public static final float INDICATION_SPEED = 2.5f;
	public static final float FALL_SPEED = 1.0f;

	/*
	 * The possible states the model can be in.
	 */
	private enum State {
		STILL,
		MOVING,
		INDICATING,
		RESETTING_UP,
		RESETTING_DOWN,
		COLORING,
		DECOLORING
	}

	private Player data;
	private ExitTileModel exitModel;
	// Animations
	private AnimationController moveAnimation;
	private BlendAnimation blendAnimation;
	private TextureAnimation textureAnimation;
	private EnumMap<TileColor, TextureAnimation> textureAnimations;
	// Private data
	private State state;
	private TileColor key;
	private float baseX, baseY;
	private int dataX, dataY;
	private boolean controllable;

	public PlayerModel(Model model, Player data, float baseX, float baseY,
					   ExitTileModel exitModel, MyGame game) {
		super(model);
		this.data = data;
		this.baseX = baseX;
		this.baseY = baseY;
		this.exitModel = exitModel;

		// Setup move and blend animations.
		moveAnimation = new AnimationController(this);
		moveAnimation.allowSameAnimation = true;
		moveAnimation.setAnimation("Cube|Fall");
		blendAnimation = new BlendAnimation(
				this, moveAnimation.current.duration, FALL_SPEED);
		blendAnimation.reset(0.0f, FALL_SPEED);

		// Build map of different texture animations.
		TextureAttribute texture = materials.first().get(
				TextureAttribute.class, TextureAttribute.Diffuse);
		textureAnimations = new EnumMap<>(TileColor.class);
		for (TileColor color : TileColor.values()) {
			textureAnimations.put(color, new TextureAnimation(
					texture, game.getPlayerAnimation(color), 1.0f));
		}
		textureAnimation = textureAnimations.get(TileColor.RED);

		// Position player and set to default state.
		dataX = data.getX();
		dataY = data.getY();
		key = data.getKey();
		updateTransform(0.0f, 0.0f);
		state = State.STILL;
	}

	/*
	 * Update the model's transform to data's position.
	 * The position can be corrected by the specified values.
	 */
	private void updateTransform(float correctX, float correctY) {
		transform.setTranslation(
				0*baseX + (data.getX() + correctX) * TileModel.SIZE,
				0.75f * TileModel.SIZE,
				0*baseY - (data.getY() + correctY) * TileModel.SIZE);
	}

	/*
	 * Trigger the movement of the model along the specified axis.
	 * Start corresponding animation if the player actually moved or not.
	 */
	private void triggerMovement(int dx, int dy, boolean moved) {
		controllable = false;
		// Update the model's position from data.
		dataX = data.getX();
		dataY = data.getY();

		// Rotate model to the axis it is moving along and update the position.
		if (dx == 0 && dy == 1) {
			transform.setToRotation(0, 1, 0, 0);
			if (moved)
				updateTransform(0, -1);
		} else if (dx == 0 && dy == -1) {
			transform.setToRotation(0, 1, 0, 180);
			if (moved)
				updateTransform(0, 1);
		} else if (dx == 1 && dy == 0) {
			transform.setToRotation(0, 1, 0, 270);
			if (moved)
				updateTransform(-1, 0);
		} else if (dx == -1 && dy == 0) {
			transform.setToRotation(0, 1, 0, 90);
			if (moved)
				updateTransform(1, 0);
		}

		// Start corresponding animation and adjust the state.
		if (moved) {
			moveAnimation.setAnimation("Cube|Movement", 1, MOVE_SPEED, this);
			state = State.MOVING;
		} else {
			updateTransform(0, 0);
			moveAnimation.setAnimation(
					"Cube|Indication", 1, INDICATION_SPEED, this);
			state = State.INDICATING;
		}
	}

	/*
	 * Initiate a coloring animation to the specified color.
	 */
	private void initiateColoring(TileColor toColor) {
		if (toColor == TileColor.NONE) {
			// Decolor the model.
			textureAnimation.resetReverse(true);
			state = State.DECOLORING;
		} else {
			// Color the model with the specified color.
			textureAnimation = textureAnimations.get(toColor);
			textureAnimation.reset(true);
			state = State.COLORING;
		}
	}

	@Override public void updateState(Object... args) {}

	@Override
	public void onEnd(AnimationController.AnimationDesc animation) {
		if (animation.animation.id.equals("Cube|Movement")) {
			// Movement has finished.
			controllable = false;
			// Reset animation and make model ready for next move.
			state = State.STILL;
			moveAnimation.current.time = 0.0f;
			transform.setToRotation(0, 1, 0, 0);
			updateTransform(0, 0);

			// Take key if model is standing on key tile.
			if (data.getKey() != key) {
				// If model takes key and is already colored, decolor first.
				if (data.getKey() != TileColor.NONE && key != TileColor.NONE) {
					initiateColoring(TileColor.NONE);
					return;
				}

				key = data.getKey();
				if (key == TileColor.NONE) {
					// Model is standing on lock tile, decolor.
					initiateColoring(TileColor.NONE);
					exitModel.releaseRequirements();
				} else {
					// Model is standing on key tile, take key color.
					initiateColoring(key);
				}
				return;
			}

			controllable = true;
		} else if (animation.animation.id.equals("Cube|Indication")) {
			// Movement indication has finished.
			controllable = false;
			// Reset animation and make model ready for next move.
			state = State.STILL;
			moveAnimation.setAnimation("Cube|Movement");
			moveAnimation.current.time = 0.0f;
			controllable = true;
		} else if (animation.animation.id.equals("Cube|Fall")) {
			// Falling/hovering has finished.
			if (state == State.RESETTING_UP) {
				// Player has hovered up; start hovering down on start tile.
				state = State.RESETTING_DOWN;
				updateTransform(0, 0.5f);
				textureAnimation.reset(false);
				moveAnimation.setAnimation("Cube|Fall", 1, -1.0f, this);
				blendAnimation.reset(0.0f, FALL_SPEED);
			} else if (state == State.RESETTING_DOWN) {
				// Player has hovered down; is ready and controllable again.
				blendAnimation.reset(1.0f, FALL_SPEED);
				state = State.STILL;
				controllable = true;
			}
		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {}

	/*
	 * Update the model's animations based on the time passed since last frame.
	 * The player becomes controllable when the animations are close to being
	 * finished, so that the control feels better for the player.
	 */
	public void update(float delta) {
		switch (state) {
			case MOVING: case INDICATING:
				moveAnimation.update(delta);
				if (!controllable && (moveAnimation.current.duration
						- moveAnimation.current.time) < CONTROL_MARGIN) {
					controllable = true;
				}
				break;
			case RESETTING_UP:
				blendAnimation.update(delta);
				moveAnimation.update(delta);
				break;
			case RESETTING_DOWN:
				blendAnimation.update(-delta);
				moveAnimation.update(delta);
				break;
			case COLORING:
				textureAnimation.update(delta);
				if (textureAnimation.getTimeLeft() < CONTROL_MARGIN
						&& !controllable) {
					controllable = true;
				} else if (!textureAnimation.isInAction()) {
					state = State.STILL;
					controllable = true;
				}
				break;
			case DECOLORING:
				textureAnimation.update(-delta);
				if (data.getKey() != key) {
					// Initiate coloring if player has to pick up key.
					if (!textureAnimation.isInAction()) {
						key = data.getKey();
						initiateColoring(key);
					}
				} else if (textureAnimation.getTimeLeft() < CONTROL_MARGIN
						&& !controllable) {
					controllable = true;
				} else if (!textureAnimation.isInAction()) {
					state = State.STILL;
					controllable = true;
				}
		}
	}

	/*
	 * Reset the model to its default state and position.
	 */
	public void reset() {
		controllable = false;

		// Update transform if player is not already at start tile.
		// I don't actually know why this has to be done.
		if (dataX != data.getX() || dataY != data.getY()) {
			Vector3 corrected = new Vector3();
			transform.getTranslation(corrected);
			corrected.z -= TileModel.SIZE / 2;
			transform.setToTranslation(corrected);
		}

		// Update the local data.
		dataX = data.getX();
		dataY = data.getY();
		key = data.getKey();
		// Start resetting animation and adjust state.
		moveAnimation.setAnimation("Cube|Fall", 1, FALL_SPEED, this);
		blendAnimation.reset(1.0f, FALL_SPEED);
		state = State.RESETTING_UP;
	}

	/*
	 * Setup the model during the start of the level.
	 */
	public void setup() {
		controllable = false;
		// I actually don't know why the transform needs to be corrected.
		updateTransform(0, 0.5f);
		// Reset animations and adjust state.
		moveAnimation.setAnimation("Cube|Fall", 1, -FALL_SPEED, this);
		blendAnimation.reset(0.0f, FALL_SPEED);
		state = State.RESETTING_DOWN;
	}

	/*
	 * Initiate a move along the specified directional axis.
	 */
	public void move(int dx, int dy) {
		controllable = false;

		// If model is already on exit tile, do not move.
		if (dataX == exitModel.getColumn() && dataY == exitModel.getRow())
			return;

		// Check if player has actually moved and trigger movement.
		boolean moved = (data.getX() != dataX || data.getY() != dataY);
		triggerMovement(dx, dy, moved);
	}

	/*
	 * Check if the model has completed the level.
	 */
	public boolean hasCompleted() {
		return (state == State.STILL
				&& dataX == exitModel.getColumn()
				&& dataY == exitModel.getRow());
	}

	/*
	 * Check if the player is occupied by some animation.
	 */
	public boolean isOccupied() {
		return (state == State.MOVING
				|| state == State.COLORING
				|| state == State.DECOLORING);
	}

	public boolean isControllable() {
		return controllable;
	}

}

