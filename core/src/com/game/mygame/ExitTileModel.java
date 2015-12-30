package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;

import java.util.ArrayList;
import java.util.EnumSet;

public class ExitTileModel extends ModelInstance
		implements Observer, AnimationController.AnimationListener {

	// The speed the model's animations are being played at.
	public static final float SPIRAL_SPEED = 1.5f;
	public static final float FALL_SPEED = 1.0f;

	/*
	 * The possible states an ExitTileModel can be in.
	 */
	private enum State {
		STILL,
		MOVING_DOWN,
		MOVING_UP,
		SNAPPING
	}

	private ExitTile data;
	private State state;
	private AnimationController moveAnimation;
	private ArrayList<RequirementModel> requirementModels;
	// Index for removed model from requirementModels.
	private int removedColor;
	// Coordinates for model translation.
	private float x, z;
	private int row, column;
	private int height;
	private int livingModels;
	private boolean onHold;

	public ExitTileModel(Model model, ExitTile data,
						 EnumSet<TileColor> requirements,
						 float x, float z, int row, int column, MyGame game) {
		super(model);
		this.data = data;
		this.x = x;
		this.z = z;
		this.row = row;
		this.column = column;
		moveAnimation = new AnimationController(this);
		moveAnimation.allowSameAnimation = true;
		moveAnimation.setAnimation("Cube|Spiral");
		height = 0;
		state = State.STILL;

		// Construct list of RequirementModels, top to bottom.
		requirementModels = new ArrayList<>();
		int h = data.getHeight() - 1;
		for (TileColor color : requirements) {
			System.out.println("Requirement: " + color);
			requirementModels.add(new RequirementModel(
					color, h, x, z, this, game));
			h--;
		}

		updateTransform(0);
	}

	/*
	 * Update the model's transform according to the height it its height.
	 */
	private void updateTransform(int height) {
		transform.setTranslation(x, (height+1) * TileModel.SIZE/2, z);
	}

	@Override
	public void updateState(Object... args) {
		// Calculate the index of the removed color for requirementModels...
		TileColor removed = (TileColor) args[0];
		for (int i = 0; i < requirementModels.size(); i++) {
			if (requirementModels.get(i).isAlive() &&
					requirementModels.get(i).getColor().equals(removed)) {
				removedColor = i;
				break;
			}
		}
		// ...and destroy it.
		requirementModels.get(removedColor).destroy();

		// Update animation and state.
		moveAnimation.setAnimation("Cube|Spiral", 1, SPIRAL_SPEED, this);
		height = data.getHeight();
		state = State.MOVING_DOWN;

		// Hold models until player model moved onto respective tile.
		holdRequirements();
		hold();
	}

	/*
	 * Note that height is oftentimes one too much, because the reversed
	 * animations start at the bottom and thus at the wrong height.
	 */
	@Override
	public void onEnd(AnimationController.AnimationDesc animation) {
		if (animation.animation.id.equals("Cube|Spiral")) {
			if (state == State.MOVING_DOWN) {
				// Spiraling down.
				livingModels--;
				if (livingModels == 0) {
					// Model is close above the board; snap to board.
					state = State.SNAPPING;
					moveAnimation.setAnimation(
							"Cube|Fall", 1, FALL_SPEED, this);
				} else {
					// Model still has space to spiral down; keep on spiraling.
					state = State.STILL;
					moveAnimation.setAnimation(
							"Cube|Spiral", 1, SPIRAL_SPEED, this);
				}
				updateTransform(height);
			} else if (state == State.MOVING_UP) {
				// Spiraling up.
				// Revive requirement model underneath current position.
				requirementModels.get(
						requirementModels.size() - livingModels - 1).revive();
				livingModels++;
				if (livingModels == data.getRequirements().size()) {
					// Model has spiraled all the way up; stop moving.
					state = State.STILL;
					moveAnimation.setAnimation(
							"Cube|Spiral", 1, SPIRAL_SPEED, this);
				} else {
					// Model still has to spiral further up; keep on spiraling.
					height++;
					moveAnimation.setAnimation(
							"Cube|Spiral", 1, -SPIRAL_SPEED, this);
				}
				updateTransform(height);
			}
		} else if (animation.animation.id.equals("Cube|Fall")) {
			if (state == State.MOVING_UP) {
				// Model snaps out of board height; spiral up.
				height = 1;
				moveAnimation.setAnimation(
						"Cube|Spiral", 1, -SPIRAL_SPEED, this);
				updateTransform(height);
			} else {
				// Model snapped to board height; stop moving.
				state = State.STILL;
				moveAnimation.setAnimation("Cube|Fall", 1, -FALL_SPEED, this);
				// Set data to alive to make it traversable.
				data.setDead(false);
			}
		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {}

	/*
	 * Update the models' animations according to time passed since last frame.
	 */
	public void update(float delta) {
		if (onHold)
			return;
		else if (state != State.STILL)
			moveAnimation.update(delta);
	}

	/*
	 * Initiate resetting the model to its default height.
	 */
	public void reset() {
		if (livingModels == data.getRequirements().size()
				&& state == State.MOVING_DOWN) {
			// Model is at the top and currently moving down; reverse movement.
			height++;
			livingModels--;
			moveAnimation.current.speed *= -1;
			state = State.MOVING_UP;
		} else {
			// Model is not at correct height yet.
			if (state == State.STILL) {
				// Model is standing still.
				if (livingModels == 0) {
					// Model is at bottom; initiate snapping animation.
					state = State.MOVING_UP;
					moveAnimation.setAnimation(
							"Cube|Fall", 1, -FALL_SPEED, this);
				} else {
					// Model is in the air; initiate spiraling animation.
					height++;
					updateTransform(height);
					moveAnimation.setAnimation(
							"Cube|Spiral", 1, -SPIRAL_SPEED, this);
					state = State.MOVING_UP;
				}
			} else if (state == State.MOVING_DOWN) {
				// Model is currently moving down; reverse movement.
				height++;
				livingModels--;
				moveAnimation.current.speed *= -1;
				state = State.MOVING_UP;
			} else if (state == State.SNAPPING) {
				// Model is currently snapping to the board; reverse snapping.
				moveAnimation.current.speed *= -1;
				state = State.MOVING_UP;
			}
		}
	}

	/*
	 * Setup the model's state and animation to bring up the board.
	 */
	public void setup() {
		height = 1;
		livingModels = 0;
		moveAnimation.setAnimation("Cube|Fall", 1, -FALL_SPEED, this);
		state = State.MOVING_UP;
	}

	/*
	 * Set the model on hold until it manually gets released.
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

	/*
	 * Set the requirement models on hold until they manually get released.
	 */
	public void holdRequirements() {
		for (RequirementModel model : requirementModels)
			model.hold();
	}

	/*
	 * Release the requirement models if they are on hold.
	 */
	public void releaseRequirements() {
		for (RequirementModel model : requirementModels)
			model.release();
	}

	/*
	 * Check if the model is at the bottom and can be traversed by the player.
	 */
	public boolean isTraversable() {
		return (height == 0 && state == State.STILL);
	}

	public ArrayList<RequirementModel> getRequirementModels() {
		return requirementModels;
	}

	public int getRow() {
		return row;
	}

	public int getColumn() {
		return column;
	}

}

