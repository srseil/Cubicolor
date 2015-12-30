package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.TextureAttribute;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;
import com.badlogic.gdx.math.Quaternion;
import com.badlogic.gdx.math.Vector3;

import java.util.EnumMap;

public class PlayerModel extends ModelInstance
		implements Observer, AnimationController.AnimationListener {

	// Keep isControllable in this class, no need for extra getter for Gamescreen...
	// -> Call move method, but dont do anything...
	// player should not be controllable until exitmodel has finished with animation, else problems.

	// The speed the model's animation are being played at.
	public static final float MOVE_SPEED = 3.0f;
	public static final float INDICATION_SPEED = 2.5f;
	public static final float FALL_SPEED = 1.0f;

	/*
	 * not needed anymore?
	 */
	private class Move {
		int dx, dy;
		boolean moved;
		public Move(int dx, int dy, boolean moved) {
			this.dx = dx;
			this.dy = dy;
			this.moved = moved;
		}
	}

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
	private State state;
	private AnimationController moveAnimation;
	private BlendAnimation blendAnimation;
	private TextureAnimation textureAnimation;

	private EnumMap<TileColor, TextureAnimation> textureAnimations;

	private TileModel[][] modelMatrix;
	private ExitTileModel exitModel;
	private float baseX, baseY;
	private int dataX, dataY, queueX, queueY;
	private TileColor key, queueKey;
	private Move queuedMove;
	private boolean controllable;

	public PlayerModel(Model model, Player data, float baseX, float baseY,
					   TileModel[][] modelMatrix, ExitTileModel exitModel,
					   MyGame game) {
		super(model);
		this.data = data;
		this.baseX = baseX;
		this.baseY = baseY;
		this.modelMatrix = modelMatrix;
		this.exitModel = exitModel;

		moveAnimation = new AnimationController(this);
		moveAnimation.allowSameAnimation = true;
		moveAnimation.setAnimation("Cube|Fall");
		blendAnimation = new BlendAnimation(this,
				moveAnimation.current.duration);

		TextureAttribute texture = materials.first().get(
				TextureAttribute.class, TextureAttribute.Diffuse);
		textureAnimations = new EnumMap<>(TileColor.class);
		for (TileColor color : TileColor.values()) {
			System.out.println(color);
			textureAnimations.put(color, new TextureAnimation(
					texture, game.getPlayerAnimation(color), 1.0f));
		}
		textureAnimation = textureAnimations.get(TileColor.RED);

		dataX = data.getX();
		dataY = data.getY();
		key = data.getKey();

		state = State.STILL;
		blendAnimation.reset(0.0f);
		updateTransform(0, 0);
	}

	private void updateTransform(float correctX, float correctY) {
		transform.setTranslation(
				0*baseX + (data.getX() + correctX) * TileModel.SIZE,
				0.75f * TileModel.SIZE,
				0*baseY - (data.getY() + correctY) * TileModel.SIZE);
	}

	private void triggerMovement(int dx, int dy, boolean moved) {
		controllable = false;
		dataX = data.getX();
		dataY = data.getY();
		/*
		System.out.println("INFO_TRIGGER: dataX: " + data.getX() + " key: " + dataX);
		System.out.println("INFO_TRIGGER: dataY: " + data.getY() + " key: " + dataY);
		*/

		if (dx == 0 && dy == 1) {
			transform.setToRotation(0, 1, 0, 0);
			if (moved) updateTransform(0, -1);
		} else if (dx == 0 && dy == -1) {
			transform.setToRotation(0, 1, 0, 180);
			if (moved) updateTransform(0, 1);
		} else if (dx == 1 && dy == 0) {
			transform.setToRotation(0, 1, 0, 270);
			if (moved) updateTransform(-1, 0);
		} else if (dx == -1 && dy == 0) {
			transform.setToRotation(0, 1, 0, 90);
			if (moved) updateTransform(1, 0);
		}

		if (moved) {
			moveAnimation.setAnimation("Cube|Movement", 1, MOVE_SPEED, this);
			// Hold exit model, gets released by requirement model.
			//exitModel.hold();
			state = State.MOVING;
		} else {
			updateTransform(0, 0);
			moveAnimation.setAnimation(
					"Cube|Indication", 1, INDICATION_SPEED, this);
			state = State.INDICATING;
		}
	}

	private void triggerQueuedMove() {
		//System.out.println("Triggering queued move.");
		if (queuedMove != null) {
			triggerMovement(queuedMove.dx, queuedMove.dy, queuedMove.moved);
			queuedMove = null;
		}
	}

	@Override public void updateState(Object... args) {
		//System.out.println("notified ");
		/*
			int dx = (int) data.getX() - dataX;
			int dy = (int) data.getY() - dataY;
			boolean moved;
			System.out.println(dx + " " + dy);

			if (dx != 0 || dy != 0)
				moved = true;
			else
				moved = false;

			if (state == State.MOVING) {
				queuedMove = new Move(dx, dy, moved);
			} else {
				triggerMovement(dx, dy, moved);
				dataX = (int) data.getX();
				dataY = (int) data.getY();
			}
			*/
	}

	@Override
	public void onEnd(AnimationController.AnimationDesc animation) {
		if (animation.animation.id.equals("Cube|Movement")) {
			controllable = false;
			//System.out.println("move is not possible... " + (queuedMove == null ? "queued" : ""));
			state = State.STILL;
			//moveAnimation.setAnimation("Cube|Movement");
			moveAnimation.current.time = 0.0f;
			transform.setToRotation(0, 1, 0, 0);
			updateTransform(0, 0);
			//exitModel.release();

			/*
			System.out.println("INFO: dataKey: " + data.getKey() + " key: " + key);
			System.out.println("INFO: dataX: " + data.getX() + " key: " + dataX);
			System.out.println("INFO: dataY: " + data.getY() + " key: " + dataY);
			*/
			// Warum die beiden letzten checks? funktioniert, weiß aber nicht genau warum...
			if (data.getKey() != key && data.getX() == dataX && data.getY() == dataY) {
				controllable = false;
				key = data.getKey();
				if (key == TileColor.NONE) {
					//System.out.println("none color");
					textureAnimation.resetReverse(true);
					state = State.DECOLORING;
				} else {
					textureAnimation = textureAnimations.get(key);
					textureAnimation.reset(true);
					state = State.COLORING;
				}
				queuedMove = null;
				exitModel.releaseRequirements();
				return;
			}

			// Not really safe...
			controllable = true;
			triggerQueuedMove();
		} else if (animation.animation.id.equals("Cube|Indication")) {
			controllable = false;
			state = State.STILL;
			moveAnimation.setAnimation("Cube|Movement");
			moveAnimation.current.time = 0.0f;
			triggerQueuedMove();
			controllable = true;
		} else if (animation.animation.id.equals("Cube|Fall")) {
			if (state == State.RESETTING_UP) {
				//System.out.println("up done");
				state = State.RESETTING_DOWN;
				//playerModel.transform.setToRotation(0, 1, 0, 0);
				updateTransform(0, 0.5f);
				textureAnimation.reset(false);
				moveAnimation.setAnimation("Cube|Fall", 1, -1.0f, this);
				blendAnimation.reset(0.0f);
			} else if (state == State.RESETTING_DOWN) {
				state = State.STILL;
				controllable = true;
				//moveAnimation.setAnimation("Cube|Movement");
			}
		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {}

	public void update(float delta) {
		switch (state) {
			case MOVING: case INDICATING:
				moveAnimation.update(delta);
				//System.out.println(moveAnimation.current.duration -
				//		moveAnimation.current.time + " ");
				if (!controllable && (moveAnimation.current.duration -
						moveAnimation.current.time) < 0.2f) {
					controllable = true;
					//System.out.println("controllable");
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
				//textureAnimations.get(key).update(delta);
				textureAnimation.update(delta);
				if (!controllable && textureAnimation.getTimeLeft() < 0.2f) {
					controllable = true;
				} else if (!textureAnimation.isInAction()) {
					// set the material color or something?
					state = State.STILL;
					controllable = true;
				}
				break;
			case DECOLORING:
				//textureAnimations.get(key).update(-delta);
				textureAnimation.update(-delta);
				if (!controllable && textureAnimation.getTimeLeft() < 0.2f) {
					controllable = true;
				} else if (!textureAnimation.isInAction()) {
					// set the material color or something?
					state = State.STILL;
					controllable = true;
				}
		}
	}

	public void reset() {
		queuedMove = null;
		controllable = false;

		// Update transform if player is not already at start tile.
		// I don't actually know why this has to be done.
		//transform.setToRotation(0, 1, 0, 0);
		if (dataX != data.getX() || dataY != data.getY()) {
			Vector3 corrected = new Vector3();
			transform.getTranslation(corrected);
			corrected.z -= TileModel.SIZE / 2;
			transform.setToTranslation(corrected);
		}

		dataX = data.getX();
		dataY = data.getY();
		key = data.getKey();

		moveAnimation.setAnimation("Cube|Fall", 1, FALL_SPEED, this);
		state = State.RESETTING_UP;
		//oldPlayerKey = TileColor.NONE;
	}

	public void setup() {
		controllable = false;
		updateTransform(0, 0.5f);

		Vector3 corrected = new Vector3();
		transform.getTranslation(corrected);
		//corrected.z -= TileModel.SIZE/2;
		Quaternion q = new Quaternion();
		transform.getRotation(q);
		System.out.println("PLAYER TRANSFORM: " + corrected + " " + q);

		moveAnimation.setAnimation("Cube|Fall", 1, -FALL_SPEED, this);
		blendAnimation.reset(0.0f);
		state = State.RESETTING_DOWN;
	}

	public void move(int dx, int dy) {
		//System.out.println("Called move: (" + (queuedMove == null ? "nope" : "yes") + ")");
		controllable = false;


		// If model is already on exit tile, do not move.
		if (dataX == exitModel.getColumn() && dataY == exitModel.getRow()) {
			return;
		}
		/*
		else if (data.getX() == exitModel.getColumn()
				&& data.getY() == exitModel.getRow()) {
			// Data is on exit tile, wait until exit model is ready.
			if (!exitModel.isTraversable()) {
				modelMatrix[dataY][dataX].hold();
				controllable = true;
				return;
			} else {
				modelMatrix[dataY][dataX].release();
			}
		}
		*/

		boolean moved;
		if (data.getX() != dataX || data.getY() != dataY)
			moved = true;
		else
			moved = false;

		if (state == State.MOVING || state == State.COLORING
				|| state == State.DECOLORING) {
			queuedMove = new Move(dx, dy, moved);
			System.out.println("MOVE QUEUED IN PLAYERMODEL");

			//controllable = true;
		}
		else {
			triggerMovement(dx, dy, moved);
			//System.out.println("Actually triggered movement");
		}
	}

	public boolean isControllable() {
		return controllable;
	}

	public boolean isOccupied() {
		return (state == State.MOVING || state == State.COLORING
				|| state == State.DECOLORING);
	}

	public boolean hasCompleted() {
		if (state == State.STILL && dataX == exitModel.getColumn() &&
				dataY == exitModel.getRow()) {
			return true;
		} else {
			return false;
		}
	}

}
