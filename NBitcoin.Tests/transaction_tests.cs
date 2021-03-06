﻿using NBitcoin.BitcoinCore;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.OpenAsset;
using NBitcoin.Policy;
using NBitcoin.Protocol;
using NBitcoin.Stealth;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NBitcoin.Tests
{
	public class transaction_tests
	{
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanParseOutpoint()
		{
			var outpoint = RandOutpoint();
			var actualOutpoint = CanParseOutpointCore(outpoint.ToString(), true);
			Assert.Equal(outpoint.Hash, actualOutpoint.Hash);
			Assert.Equal(outpoint.N, actualOutpoint.N);
			CanParseOutpointCore("abc-6", false);
			CanParseOutpointCore("bdaea31696b464c678c4bcc5d0565d58c86bb00c29f96bb86d1278c510d50aet-6", false);
			CanParseOutpointCore("bdaea31696b464c678c4bcc5d0565d58c86bb00c29f96bb86d1278c510d50aea-6", true);
			CanParseOutpointCore("bdaea31696b464c678c4bcc5d0565d58c86bb00c29f96bb86d1278c510d50aeaf-6", false);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanGetMedianBlock()
		{
			ConcurrentChain chain = new ConcurrentChain(Network.Main);
			DateTimeOffset now = DateTimeOffset.UtcNow;
			chain.SetTip(CreateBlock(now, 0, chain));
			chain.SetTip(CreateBlock(now, -1, chain));
			chain.SetTip(CreateBlock(now, 1, chain));
			Assert.Equal(CreateBlock(now, 0).Header.BlockTime, chain.Tip.GetMedianTimePast()); // x -1 0 1
			chain.SetTip(CreateBlock(now, 2, chain));
			Assert.Equal(CreateBlock(now, 0).Header.BlockTime, chain.Tip.GetMedianTimePast()); // x -1 0 1 2
			chain.SetTip(CreateBlock(now, 3, chain));
			Assert.Equal(CreateBlock(now, 1).Header.BlockTime, chain.Tip.GetMedianTimePast()); // x -1 0 1 2 3
			chain.SetTip(CreateBlock(now, 4, chain));
			chain.SetTip(CreateBlock(now, 5, chain));
			chain.SetTip(CreateBlock(now, 6, chain));
			chain.SetTip(CreateBlock(now, 7, chain));
			chain.SetTip(CreateBlock(now, 8, chain));

			Assert.Equal(CreateBlock(now, 3).Header.BlockTime, chain.Tip.GetMedianTimePast()); // x -1 0 1 2 3 4 5 6 7 8

			chain.SetTip(CreateBlock(now, 9, chain));
			Assert.Equal(CreateBlock(now, 4).Header.BlockTime, chain.Tip.GetMedianTimePast()); // x -1 0 1 2 3 4 5 6 7 8 9
			chain.SetTip(CreateBlock(now, 10, chain));
			Assert.Equal(CreateBlock(now, 5).Header.BlockTime, chain.Tip.GetMedianTimePast()); // x -1 0 1 2 3 4 5 6 7 8 9 10
		}

		private ChainedBlock CreateBlock(DateTimeOffset now, int offset, ChainBase chain = null)
		{
			Block b = new Block(new BlockHeader()
			{
				BlockTime = now + TimeSpan.FromMinutes(offset)
			});
			if(chain != null)
			{
				b.Header.HashPrevBlock = chain.Tip.HashBlock;
				return new ChainedBlock(b.Header, null, chain.Tip);
			}
			else
				return new ChainedBlock(b.Header, 0);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanDetectFinalTransaction()
		{
			Transaction tx = new Transaction();
			tx.Inputs.Add(new TxIn());
			tx.Inputs[0].Sequence = 1;
			Assert.True(tx.IsFinal(null));

			//Test on date, normal case
			tx.LockTime = new LockTime(new DateTimeOffset(2012, 8, 18, 0, 0, 0, TimeSpan.Zero));
			var time = tx.LockTime.Date;
			Assert.False(tx.IsFinal(null));
			Assert.True(tx.IsFinal(time + TimeSpan.FromSeconds(1), 0));
			Assert.False(tx.IsFinal(time, 0));
			Assert.False(tx.IsFinal(time - TimeSpan.FromSeconds(1), 0));
			tx.Inputs[0].Sequence = uint.MaxValue;
			Assert.True(tx.IsFinal(time, 0));
			Assert.True(tx.IsFinal(time - TimeSpan.FromSeconds(1), 0));
			tx.Inputs[0].Sequence = 1;
			//////////

			//Test on heigh, normal case
			tx.LockTime = new LockTime(400);
			DateTimeOffset zero = Utils.UnixTimeToDateTime(0);
			Assert.False(tx.IsFinal(zero, 0));
			Assert.False(tx.IsFinal(zero, 400));
			Assert.True(tx.IsFinal(zero, 401));
			Assert.False(tx.IsFinal(zero, 399));
			//////////

			//Edge
			tx.LockTime = new LockTime(LockTime.LOCKTIME_THRESHOLD);
			time = tx.LockTime.Date;
			Assert.False(tx.IsFinal(null));
			Assert.True(tx.IsFinal(time + TimeSpan.FromSeconds(1), 0));
			Assert.False(tx.IsFinal(time, 0));
			Assert.False(tx.IsFinal(time - TimeSpan.FromSeconds(1), 0));
			tx.Inputs[0].Sequence = uint.MaxValue;
			Assert.True(tx.IsFinal(time, 0));
			Assert.True(tx.IsFinal(time - TimeSpan.FromSeconds(1), 0));
			tx.Inputs[0].Sequence = 1;
			//////////
		}

		private OutPoint CanParseOutpointCore(string str, bool valid)
		{
			try
			{
				var result = OutPoint.Parse(str);
				Assert.True(valid);
				return result;
			}
			catch
			{
				Assert.False(valid);
				return null;
			}
		}
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanExtractTxOutDestinationEasily()
		{
			var secret = new BitcoinSecret("KyJTjvFpPF6DDX4fnT56d2eATPfxjdUPXFFUb85psnCdh34iyXRQ");

			var tx = new Transaction();
			var p2pkh = new TxOut(new Money((UInt64)45000000), secret.GetAddress());
			var p2pk = new TxOut(new Money((UInt64)80000000), secret.PrivateKey.PubKey);

			tx.AddOutput(p2pkh);
			tx.AddOutput(p2pk);

			Assert.False(p2pkh.IsTo(secret.PrivateKey.PubKey));
			Assert.True(p2pkh.IsTo(secret.GetAddress()));
			Assert.True(p2pk.IsTo(secret.PrivateKey.PubKey));
			Assert.False(p2pk.IsTo(secret.GetAddress()));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanSignTransaction()
		{
			var key = new Key();
			var scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(key.PubKey);

			Transaction tx = new Transaction();
			tx.AddInput(new TxIn(new OutPoint(tx.GetHash(), 0))
			{
				ScriptSig = scriptPubKey
			});
			tx.AddInput(new TxIn(new OutPoint(tx.GetHash(), 1))
			{
				ScriptSig = scriptPubKey
			});
			tx.AddOutput(new TxOut("21", key.PubKey.Hash));
			var clone = tx.Clone();
			tx.Sign(key, false);
			AssertCorrectlySigned(tx, scriptPubKey);
			clone.Sign(key, true);
			AssertCorrectlySigned(clone, scriptPubKey.Hash.ScriptPubKey);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanSelectCoin()
		{
			var selector = new DefaultCoinSelector(0);
			Assert.Null(selector.Select(new ICoin[] { CreateCoin("9") }, Money.Parse("10.0")));
			Assert.NotNull(selector.Select(new ICoin[] { CreateCoin("9"), CreateCoin("1") }, Money.Parse("10.0")));
			Assert.NotNull(selector.Select(new ICoin[] { CreateCoin("10.0") }, Money.Parse("10.0")));
			Assert.NotNull(selector.Select(new ICoin[] 
			{ 
				CreateCoin("5.0"),
				CreateCoin("4.0"),
				CreateCoin("11.0"),
			}, Money.Parse("10.0")));

			Assert.NotNull(selector.Select(new ICoin[] 
			{ 
				CreateCoin("3.0"),
				CreateCoin("3.0"),
				CreateCoin("3.0"),
				CreateCoin("3.0"),
				CreateCoin("3.0")
			}, Money.Parse("10.0")));
		}

		private Coin CreateCoin(Money amount)
		{
			return new Coin(new OutPoint(Rand(), 0), new TxOut()
			{
				Value = amount
			});
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanBuildIssueColoredCoinWithMultiSigP2SH()
		{
			var satoshi = new Key();
			var bob = new Key();
			var alice = new Key();

			var goldRedeem = PayToMultiSigTemplate.Instance
									.GenerateScriptPubKey(2, new[] { satoshi.PubKey, bob.PubKey, alice.PubKey });

			var goldScriptPubKey = goldRedeem.Hash.ScriptPubKey;
			var goldAssetId = goldScriptPubKey.Hash.ToAssetId();

			var issuanceCoin = new IssuanceCoin(
				new ScriptCoin(RandOutpoint(), new TxOut(new Money(2880), goldScriptPubKey), goldRedeem));

			var nico = new Key();

			var bobSigned =
				new TransactionBuilder()
				.AddCoins(issuanceCoin)
				.AddKeys(bob)
				.IssueAsset(nico.PubKey, new AssetMoney(goldAssetId, 1000))
				.BuildTransaction(true);

			var aliceSigned =
				new TransactionBuilder()
					.AddCoins(issuanceCoin)
					.AddKeys(alice)
					.SignTransaction(bobSigned);

			Assert.True(
				new TransactionBuilder()
				{
					StandardTransactionPolicy = EasyPolicy
				}
					.AddCoins(issuanceCoin)
					.Verify(aliceSigned));

			//In one two one line

			var builder = new TransactionBuilder();
			builder.StandardTransactionPolicy = RelayPolicy.Clone();
			builder.StandardTransactionPolicy.CheckFee = false;
			var tx =
				builder
				.AddCoins(issuanceCoin)
				.AddKeys(alice, satoshi)
				.IssueAsset(nico.PubKey, new AssetMoney(goldAssetId, 1000))
				.BuildTransaction(true);
			Assert.True(builder.Verify(tx));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		//https://github.com/NicolasDorier/NBitcoin/issues/34
		public void CanBuildAnyoneCanPayTransaction()
		{
			//Carla is buying from Alice. Bob is acting as a mediator between Alice and Carla.
			var aliceKey = new Key();
			var bobKey = new Key();
			var carlaKey = new Key();

			// Alice + Bob 2 of 2 multisig "wallet"
			var aliceBobRedeemScript = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, new PubKey[] { aliceKey.PubKey, bobKey.PubKey });

			var txBuilder = new TransactionBuilder();
			var funding = txBuilder
				.AddCoins(GetCoinSource(aliceKey))
				.AddKeys(aliceKey)
				.Send(aliceBobRedeemScript.Hash, "0.5")
				.SetChange(aliceKey.PubKey.Hash)
				.SendFees(Money.Satoshis(5000))
				.BuildTransaction(true);

			Assert.True(txBuilder.Verify(funding));

			List<ICoin> aliceBobCoins = new List<ICoin>();
			aliceBobCoins.Add(new ScriptCoin(funding, funding.Outputs.To(aliceBobRedeemScript.Hash).First(), aliceBobRedeemScript));

			// first Bob constructs the TX
			txBuilder = new TransactionBuilder();
			var unsigned = txBuilder
				// spend from the Alice+Bob wallet to Carla
				.AddCoins(aliceBobCoins)
				.Send(carlaKey.PubKey.Hash, "0.01")
				//and Carla pays Alice
				.Send(aliceKey.PubKey.Hash, "0.02")
				.CoverOnly("0.01")
				.SetChange(aliceBobRedeemScript.Hash)
				// Bob does not sign anything yet
				.BuildTransaction(false);

			Assert.True(unsigned.Outputs.Count == 3);
			Assert.True(unsigned.Outputs[0].IsTo(aliceBobRedeemScript.Hash));
			//Only 0.01 should be covered, not 0.03 so 0.49 goes back to Alice+Bob
			Assert.True(unsigned.Outputs[0].Value == Money.Parse("0.49"));


			Assert.True(unsigned.Outputs[1].IsTo(carlaKey.PubKey.Hash));
			Assert.True(unsigned.Outputs[1].Value == Money.Parse("0.01"));

			Assert.True(unsigned.Outputs[2].IsTo(aliceKey.PubKey.Hash));
			Assert.True(unsigned.Outputs[2].Value == Money.Parse("0.02"));

			//Alice signs	
			txBuilder = new TransactionBuilder();
			var aliceSigned = txBuilder
					.AddCoins(aliceBobCoins)
					.AddKeys(aliceKey)
					.SignTransaction(unsigned, SigHash.All | SigHash.AnyoneCanPay);

			var carlaCoins = GetCoinSource(carlaKey, "1.0", "0.8", "0.6", "0.2", "0.05");

			//Scenario 1 : Carla knows aliceBobCoins so she can calculate how much coin she need to complete the transaction
			//Carla fills and signs
			txBuilder = new TransactionBuilder();
			var carlaSigned = txBuilder
				.AddCoins(aliceBobCoins)
				.Then()
				.AddKeys(carlaKey)
				//Carla should complete 0.02, but with 0.03 of fees, she should have a coins of 0.05
				.AddCoins(carlaCoins)
				.ContinueToBuild(aliceSigned)
				.SendFees("0.03")
				.CoverTheRest()
				.BuildTransaction(true);


			//Bob review and signs
			txBuilder = new TransactionBuilder();
			var bobSigned = txBuilder
				.AddCoins(aliceBobCoins)
				.AddKeys(bobKey)
				.SignTransaction(carlaSigned);

			txBuilder.AddCoins(carlaCoins);
			Assert.True(txBuilder.Verify(bobSigned));


			//Scenario 2 : Carla is told by Bob to complete 0.05 BTC
			//Carla fills and signs
			txBuilder = new TransactionBuilder();
			carlaSigned = txBuilder
				.AddKeys(carlaKey)
				.AddCoins(carlaCoins)
				//Carla should complete 0.02, but with 0.03 of fees, she should have a coins of 0.05
				.ContinueToBuild(aliceSigned)
				.CoverOnly("0.05")
				.BuildTransaction(true);


			//Bob review and signs
			txBuilder = new TransactionBuilder();
			bobSigned = txBuilder
				.AddCoins(aliceBobCoins)
				.AddKeys(bobKey)
				.SignTransaction(carlaSigned);

			txBuilder.AddCoins(carlaCoins);
			Assert.True(txBuilder.Verify(bobSigned));
		}

		private ICoin[] GetCoinSource(Key destination, params Money[] amounts)
		{
			if(amounts.Length == 0)
				amounts = new[] { Money.Parse("100.0") };

			return amounts
				.Select(a => new Coin(RandOutpoint(), new TxOut(a, destination.PubKey.Hash)))
				.ToArray();
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanBuildShuffleColoredTransaction()
		{
			var gold = new Key();
			var silver = new Key();
			var goldId = gold.PubKey.ScriptPubKey.Hash.ToAssetId();
			var silverId = silver.PubKey.ScriptPubKey.Hash.ToAssetId();

			var satoshi = new Key();
			var bob = new Key();

			var repo = new NoSqlColoredTransactionRepository(new NoSqlTransactionRepository(), new InMemoryNoSqlRepository());

			var init = new Transaction()
			{
				Outputs =
				{
					new TxOut("1.0", gold.PubKey),
					new TxOut("1.0", silver.PubKey),
					new TxOut("1.0", satoshi.PubKey)
				}
			};
			repo.Transactions.Put(init.GetHash(), init);

			var issuanceCoins =
				init
				.Outputs
				.Take(2)
				.Select((o, i) => new IssuanceCoin(new OutPoint(init.GetHash(), i), init.Outputs[i]))
				.OfType<ICoin>().ToArray();

			var satoshiBTC = new Coin(new OutPoint(init.GetHash(), 2), init.Outputs[2]);

			var coins = new List<ICoin>();
			coins.AddRange(issuanceCoins);
			var txBuilder = new TransactionBuilder(1);
			txBuilder.StandardTransactionPolicy = RelayPolicy;
			//Can issue gold to satoshi and bob
			var tx = txBuilder
				.AddCoins(coins.ToArray())
				.AddKeys(gold)
				.IssueAsset(satoshi.PubKey, new AssetMoney(goldId, 1000))
				.IssueAsset(bob.PubKey, new AssetMoney(goldId, 500))
				.SendFees("0.1")
				.SetChange(gold.PubKey)
				.BuildTransaction(true);
			Assert.True(txBuilder.Verify(tx, "0.1"));

			//Ensure BTC from the IssuanceCoin are returned
			Assert.Equal(Money.Parse("0.89994240"), tx.Outputs[2].Value);
			Assert.Equal(gold.PubKey.ScriptPubKey, tx.Outputs[2].ScriptPubKey);

			//Can issue and send in same transaction
			repo.Transactions.Put(tx.GetHash(), tx);


			var cc = ColoredCoin.Find(tx, repo);
			for(int i = 0 ; i < 20 ; i++)
			{
				txBuilder = new TransactionBuilder(i);
				txBuilder.StandardTransactionPolicy = RelayPolicy;
				tx = txBuilder
					.AddCoins(satoshiBTC)
					.AddCoins(cc)
					.AddKeys(satoshi)
					.SendAsset(gold, new AssetMoney(goldId, 10))
					.SetChange(satoshi)
					.Then()
					.AddKeys(gold)
					.AddCoins(issuanceCoins)
					.IssueAsset(bob, new AssetMoney(goldId, 1))
					.SetChange(gold)
					.Shuffle()
					.BuildTransaction(true);

				repo.Transactions.Put(tx.GetHash(), tx);

				var ctx = tx.GetColoredTransaction(repo);
				Assert.Equal(1, ctx.Issuances.Count);
				Assert.Equal(2, ctx.Transfers.Count);
			}
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanBuildColoredTransaction()
		{
			var gold = new Key();
			var silver = new Key();
			var goldId = gold.PubKey.ScriptPubKey.Hash.ToAssetId();
			var silverId = silver.PubKey.ScriptPubKey.Hash.ToAssetId();

			var satoshi = new Key();
			var bob = new Key();
			var alice = new Key();

			var repo = new NoSqlColoredTransactionRepository();

			var init = new Transaction()
			{
				Outputs =
				{
					new TxOut("1.0", gold.PubKey),
					new TxOut("1.0", silver.PubKey),
					new TxOut("1.0", satoshi.PubKey)
				}
			};

			repo.Transactions.Put(init);

			var issuanceCoins =
				init
				.Outputs
				.AsCoins()
				.Take(2)
				.Select((c, i) => new IssuanceCoin(c))
				.OfType<ICoin>().ToArray();

			var satoshiBTC = init.Outputs.AsCoins().Last();

			var coins = new List<ICoin>();
			coins.AddRange(issuanceCoins);
			var txBuilder = new TransactionBuilder();
			txBuilder.StandardTransactionPolicy = RelayPolicy;
			//Can issue gold to satoshi and bob
			var tx = txBuilder
				.AddCoins(coins.ToArray())
				.AddKeys(gold)
				.IssueAsset(satoshi.PubKey, new AssetMoney(goldId, 1000))
				.IssueAsset(bob.PubKey, new AssetMoney(goldId, 500))
				.SendFees("0.1")
				.SetChange(gold.PubKey)
				.BuildTransaction(true);
			Assert.True(txBuilder.Verify(tx, "0.1"));

			//Ensure BTC from the IssuanceCoin are returned
			Assert.Equal(Money.Parse("0.89994240"), tx.Outputs[2].Value);
			Assert.Equal(gold.PubKey.ScriptPubKey, tx.Outputs[2].ScriptPubKey);

			repo.Transactions.Put(tx);

			var colored = tx.GetColoredTransaction(repo);
			Assert.Equal(2, colored.Issuances.Count);
			Assert.True(colored.Issuances.All(i => i.Asset.Id == goldId));
			AssertHasAsset(tx, colored, colored.Issuances[0], goldId, 500, bob.PubKey);
			AssertHasAsset(tx, colored, colored.Issuances[1], goldId, 1000, satoshi.PubKey);

			var coloredCoins = ColoredCoin.Find(tx, colored).ToArray();
			Assert.Equal(2, coloredCoins.Length);

			//Can issue silver to bob, and send some gold to satoshi
			coins.Add(coloredCoins.First(c => c.ScriptPubKey == bob.PubKey.ScriptPubKey));
			txBuilder = new TransactionBuilder();
			txBuilder.StandardTransactionPolicy = EasyPolicy;
			tx = txBuilder
				.AddCoins(coins.ToArray())
				.AddKeys(silver, bob)
				.SetChange(bob.PubKey)
				.IssueAsset(bob.PubKey, new AssetMoney(silverId, 10))
				.SendAsset(satoshi.PubKey, new AssetMoney(goldId, 30))
				.BuildTransaction(true);

			Assert.True(txBuilder.Verify(tx));
			colored = tx.GetColoredTransaction(repo);
			Assert.Equal(1, colored.Inputs.Count);
			Assert.Equal(goldId, colored.Inputs[0].Asset.Id);
			Assert.Equal(500, colored.Inputs[0].Asset.Quantity);
			Assert.Equal(1, colored.Issuances.Count);
			Assert.Equal(2, colored.Transfers.Count);
			AssertHasAsset(tx, colored, colored.Transfers[0], goldId, 470, bob.PubKey);
			AssertHasAsset(tx, colored, colored.Transfers[1], goldId, 30, satoshi.PubKey);

			repo.Transactions.Put(tx);


			//Can swap : 
			//satoshi wants to send 100 gold to bob 
			//bob wants to send 200 silver, 5 gold and 0.9 BTC to satoshi

			//Satoshi receive gold
			txBuilder = new TransactionBuilder();
			txBuilder.StandardTransactionPolicy = RelayPolicy;
			tx = txBuilder
					.AddKeys(gold)
					.AddCoins(issuanceCoins)
					.IssueAsset(satoshi.PubKey, new AssetMoney(goldId, 1000UL))
					.SetChange(gold.PubKey)
					.SendFees(Money.Coins(0.0004m))
					.BuildTransaction(true);
			Assert.True(txBuilder.Verify(tx));
			repo.Transactions.Put(tx);
			var satoshiCoin = ColoredCoin.Find(tx, repo).First();


			//Gold receive 2.5 BTC
			tx = new Transaction()
			{
				Outputs =
				{
					new TxOut("2.5",gold.PubKey)
				}
			};
			repo.Transactions.Put(tx.GetHash(), tx);

			//Bob receive silver and 2 btc
			txBuilder = new TransactionBuilder();
			txBuilder.StandardTransactionPolicy = RelayPolicy;
			tx = txBuilder
					.AddKeys(silver, gold)
					.AddCoins(issuanceCoins)
					.AddCoins(new Coin(new OutPoint(tx.GetHash(), 0), new TxOut("2.5", gold.PubKey.ScriptPubKey)))
					.IssueAsset(bob.PubKey, new AssetMoney(silverId, 300UL))
					.Send(bob.PubKey, "2.00")
					.SendFees(Money.Coins(0.0004m))
					.SetChange(gold.PubKey)
					.BuildTransaction(true);
			Assert.True(txBuilder.Verify(tx));
			repo.Transactions.Put(tx);

			var bobSilverCoin = ColoredCoin.Find(tx, repo).First();
			var bobBitcoin = new Coin(new OutPoint(tx.GetHash(), 2), tx.Outputs[2]);

			//Bob receive gold
			txBuilder = new TransactionBuilder();
			txBuilder.StandardTransactionPolicy = RelayPolicy;
			tx = txBuilder
					.AddKeys(gold)
					.AddCoins(issuanceCoins)
					.IssueAsset(bob.PubKey, new AssetMoney(goldId, 50UL))
					.SetChange(gold.PubKey)
					.SendFees(Money.Coins(0.0004m))
					.BuildTransaction(true);
			Assert.True(txBuilder.Verify(tx));
			repo.Transactions.Put(tx.GetHash(), tx);

			var bobGoldCoin = ColoredCoin.Find(tx, repo).First();

			txBuilder = new TransactionBuilder();
			txBuilder.StandardTransactionPolicy = RelayPolicy;
			tx = txBuilder
				.AddCoins(satoshiCoin)
				.AddCoins(satoshiBTC)
				.SendAsset(bob.PubKey, new AssetMoney(goldId, 100))
				.SendFees(Money.Coins(0.0004m))
				.SetChange(satoshi.PubKey)
				.Then()
				.AddCoins(bobSilverCoin, bobGoldCoin, bobBitcoin)
				.SendAsset(satoshi.PubKey, new AssetMoney(silverId, 200))
				.Send(satoshi.PubKey, "0.9")
				.SendAsset(satoshi.PubKey, new AssetMoney(goldId, 5))
				.SetChange(bob.PubKey)
				.BuildTransaction(false);

			colored = tx.GetColoredTransaction(repo);

			AssertHasAsset(tx, colored, colored.Inputs[0], goldId, 1000, null);
			AssertHasAsset(tx, colored, colored.Inputs[1], silverId, 300, null);

			AssertHasAsset(tx, colored, colored.Transfers[0], goldId, 900, satoshi.PubKey);
			AssertHasAsset(tx, colored, colored.Transfers[1], goldId, 100, bob.PubKey);

			AssertHasAsset(tx, colored, colored.Transfers[2], silverId, 100, bob.PubKey);
			AssertHasAsset(tx, colored, colored.Transfers[3], silverId, 200, satoshi.PubKey);

			AssertHasAsset(tx, colored, colored.Transfers[4], goldId, 45, bob.PubKey);
			AssertHasAsset(tx, colored, colored.Transfers[5], goldId, 5, satoshi.PubKey);

			Assert.True(tx.Outputs[8].Value == Money.Parse("1.0999424"));
			Assert.True(tx.Outputs[8].ScriptPubKey == bob.PubKey.ScriptPubKey);
			Assert.True(tx.Outputs[9].Value == Money.Parse("0.9"));
			Assert.True(tx.Outputs[9].ScriptPubKey == satoshi.PubKey.ScriptPubKey);

			tx = txBuilder.AddKeys(satoshi, bob).SignTransaction(tx);
			Assert.True(txBuilder.Verify(tx));


			//Bob send coins to Satoshi, but alice pay for the dust
			var funding =
				new TransactionBuilder()
				{
					StandardTransactionPolicy = RelayPolicy
				}
				.AddCoins(issuanceCoins)
				.AddKeys(gold)
				.IssueAsset(bob.PubKey.Hash, new AssetMoney(goldId, 100UL))
				.SetChange(gold.PubKey.Hash)
				.SendFees(Money.Coins(0.0004m))
				.BuildTransaction(true);

			repo.Transactions.Put(funding);

			var bobGold = ColoredCoin.Find(funding, repo).ToArray();

			Transaction transfer = null;
			try
			{
				transfer =
					new TransactionBuilder()
					{
						StandardTransactionPolicy = RelayPolicy
					}
					.AddCoins(bobGold)
					.SendAsset(alice.PubKey.Hash, new AssetMoney(goldId, 40UL))
					.SetChange(bob.PubKey.Hash)
					.BuildTransaction(true);
				Assert.False(true, "Should have thrown");
			}
			catch(NotEnoughFundsException ex) //Not enough dust to send the change
			{
				Assert.True(((Money)ex.Missing).Satoshi == 2730);
				var rate = new FeeRate(Money.Coins(0.0004m));
				txBuilder = new TransactionBuilder();
				txBuilder.StandardTransactionPolicy = RelayPolicy;
				transfer =
					txBuilder
					.AddCoins(bobGold)
					.AddCoins(((IssuanceCoin)issuanceCoins[0]).Bearer)
					.AddKeys(gold, bob)
					.SendAsset(alice.PubKey, new AssetMoney(goldId, 40UL))
					.SetChange(bob.PubKey, ChangeType.Colored)
					.SetChange(gold.PubKey.Hash, ChangeType.Uncolored)
					.SendEstimatedFees(rate)
					.BuildTransaction(true);
				var fee = transfer.GetFee(txBuilder.FindSpentCoins(transfer));
				Assert.True(txBuilder.Verify(transfer, fee));

				repo.Transactions.Put(funding.GetHash(), funding);

				colored = ColoredTransaction.FetchColors(transfer, repo);
				AssertHasAsset(transfer, colored, colored.Transfers[0], goldId, 60, bob.PubKey);
				AssertHasAsset(transfer, colored, colored.Transfers[1], goldId, 40, alice.PubKey);

				var change = transfer.Outputs.Last(o => o.ScriptPubKey == gold.PubKey.Hash.ScriptPubKey);
				Assert.Equal(Money.Coins(0.99980450m), change.Value);

				Assert.Equal(gold.PubKey.Hash, change.ScriptPubKey.GetDestination());

				//Verify issuancecoin can have an url
				var issuanceCoin = (IssuanceCoin)issuanceCoins[0];
				issuanceCoin.DefinitionUrl = new Uri("http://toto.com/");
				txBuilder = new TransactionBuilder();
				tx = txBuilder
					.AddKeys(gold)
					.AddCoins(issuanceCoin)
					.IssueAsset(bob, new AssetMoney(gold.PubKey, 10))
					.SetChange(gold)
					.BuildTransaction(true);

				Assert.Equal("http://toto.com/", tx.GetColoredMarker().GetMetadataUrl().AbsoluteUri);
			}
		}

		private void AssertHasAsset(Transaction tx, ColoredTransaction colored, ColoredEntry entry, AssetId assetId, int quantity, PubKey destination)
		{
			var txout = tx.Outputs[entry.Index];
			Assert.True(entry.Asset.Id == assetId);
			Assert.True(entry.Asset.Quantity == quantity);
			if(destination != null)
				Assert.True(txout.ScriptPubKey == destination.ScriptPubKey);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanBuildStealthTransaction()
		{
			var stealthKeys = Enumerable.Range(0, 3).Select(_ => new Key()).ToArray();
			var scanKey = new Key();

			var darkSatoshi = new BitcoinStealthAddress(scanKey.PubKey, stealthKeys.Select(k => k.PubKey).ToArray(), 2, new BitField(3, 5), Network.Main);

			var bob = new Key();
			var coins = new Coin[] { 
				new Coin() 
				{ 
					Outpoint = RandOutpoint(),
					TxOut = new TxOut("1.00",bob.PubKey.Hash)
				} };

			//Bob sends money to satoshi
			TransactionBuilder builder = new TransactionBuilder();
			builder.StandardTransactionPolicy = EasyPolicy;
			var tx =
				builder
				.AddCoins(coins)
				.AddKeys(bob)
				.Send(darkSatoshi, "1.00")
				.BuildTransaction(true);
			Assert.True(builder.Verify(tx));

			//Satoshi scans a StealthCoin in the transaction with his scan key
			var stealthCoin = StealthCoin.Find(tx, darkSatoshi, scanKey);
			Assert.NotNull(stealthCoin);

			//Satoshi sends back the money to Bob
			builder = new TransactionBuilder();
			builder.StandardTransactionPolicy = EasyPolicy;
			tx =
				builder
					.AddCoins(stealthCoin)
					.AddKeys(stealthKeys)
					.AddKeys(scanKey)
					.Send(bob.PubKey.Hash, "1.00")
					.BuildTransaction(true);

			Assert.True(builder.Verify(tx)); //Signed !


			//Same scenario, Satoshi wants to send money back to Bob
			//However, his keys are spread on two machines
			//He partially signs on the 1st machine
			builder = new TransactionBuilder();
			builder.StandardTransactionPolicy = EasyPolicy;
			tx =
				builder
					.AddCoins(stealthCoin)
					.AddKeys(stealthKeys.Skip(2).ToArray()) //Only one Stealth Key
					.AddKeys(scanKey)
					.Send(bob.PubKey.Hash, "1.00")
					.BuildTransaction(true);

			Assert.False(builder.Verify(tx)); //Not fully signed

			//Then he partially signs on the 2nd machine
			builder = new TransactionBuilder();
			builder.StandardTransactionPolicy = EasyPolicy;
			tx =
				builder
					.AddCoins(stealthCoin)
					.AddKeys(stealthKeys[0]) //Other key
					.AddKeys(scanKey)
					.SignTransaction(tx);

			Assert.True(builder.Verify(tx)); //Fully signed !
		}

		private OutPoint RandOutpoint()
		{
			return new OutPoint(Rand(), 0);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanSplitFees()
		{
			var satoshi = new Key();
			var alice = new Key();
			var bob = new Key();

			var aliceCoins = new ICoin[] { RandomCoin("0.4", alice), RandomCoin("0.6", alice) };
			var bobCoins = new ICoin[] { RandomCoin("0.2", bob), RandomCoin("0.3", bob) };

			TransactionBuilder builder = new TransactionBuilder();
			FeeRate rate = new FeeRate(Money.Coins(0.0004m));
			var tx = builder
				.AddCoins(aliceCoins)
				.AddKeys(alice)
				.Send(satoshi, Money.Coins(0.1m))
				.SetChange(alice)
				.Then()
				.AddCoins(bobCoins)
				.AddKeys(bob)
				.Send(satoshi, Money.Coins(0.01m))
				.SetChange(bob)
				.SendEstimatedFeesSplit(rate)
				.BuildTransaction(true);

			var estimated = builder.EstimateFees(tx, rate);

			Assert.True(builder.Verify(tx, estimated));

			// Alice should pay two times more fee than bob
			builder = new TransactionBuilder();
			tx = builder
				.AddCoins(aliceCoins)
				.AddKeys(alice)
				.SetFeeWeight(2.0m)
				.Send(satoshi, Money.Coins(0.1m))
				.SetChange(alice)
				.Then()
				.AddCoins(bobCoins)
				.AddKeys(bob)
				.Send(satoshi, Money.Coins(0.01m))
				.SetChange(bob)
				.SendFeesSplit(Money.Coins(0.6m))
				.BuildTransaction(true);

			var spentAlice = builder.FindSpentCoins(tx).Where(c => aliceCoins.Contains(c)).OfType<Coin>().Select(c => c.Amount).Sum();
			var receivedAlice = tx.Outputs.AsCoins().Where(c => c.ScriptPubKey == alice.PubKey.Hash.ScriptPubKey).Select(c => c.Amount).Sum();
			Assert.Equal(Money.Coins(0.1m + 0.4m), spentAlice - receivedAlice);

			var spentBob = builder.FindSpentCoins(tx).Where(c => bobCoins.Contains(c)).OfType<Coin>().Select(c => c.Amount).Sum();
			var receivedBob = tx.Outputs.AsCoins().Where(c => c.ScriptPubKey == bob.PubKey.Hash.ScriptPubKey).Select(c => c.Amount).Sum();
			Assert.Equal(Money.Coins(0.01m + 0.2m), spentBob - receivedBob);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanVerifySequenceLock()
		{
			var now = new DateTimeOffset(1988, 7, 18, 0, 0, 0, TimeSpan.Zero);
			var step = TimeSpan.FromMinutes(10.0);
			var smallStep = new Sequence(step).LockPeriod;
			CanVerifySequenceLockCore(new[] { new Sequence(1) }, new[] { 1 }, 2, now, true, new SequenceLock(1, -1));
			CanVerifySequenceLockCore(new[] { new Sequence(1) }, new[] { 1 }, 1, now, false, new SequenceLock(1, -1));
			CanVerifySequenceLockCore(
				new[] 
				{ 
					new Sequence(1),
					new Sequence(5),
					new Sequence(11),
					new Sequence(8)
				},
				new[] { 1, 5, 7, 9 }, 10, DateTimeOffset.UtcNow, false, new SequenceLock(17, -1));

			CanVerifySequenceLockCore(
				new[] 
				{ 
					new Sequence(smallStep), //MTP(block[11] is +60min) 
				},
				new[] { 12 }, 13, now, true, new SequenceLock(-1, now + TimeSpan.FromMinutes(60.0) + smallStep - TimeSpan.FromSeconds(1)));

			CanVerifySequenceLockCore(
				new[] 
				{ 
					new Sequence(smallStep), //MTP(block[11] is +60min) 
				},
				new[] { 12 }, 12, now, false, new SequenceLock(-1, now + TimeSpan.FromMinutes(60.0) + smallStep - TimeSpan.FromSeconds(1)));
		}

		private void CanVerifySequenceLockCore(Sequence[] sequences, int[] prevHeights, int currentHeight, DateTimeOffset first, bool expected, SequenceLock expectedLock)
		{
			ConcurrentChain chain = new ConcurrentChain(new BlockHeader()
			{
				BlockTime = first
			});
			first = first + TimeSpan.FromMinutes(10);
			while(currentHeight != chain.Height)
			{
				chain.SetTip(new BlockHeader()
				{
					BlockTime = first,
					HashPrevBlock = chain.Tip.HashBlock
				});
				first = first + TimeSpan.FromMinutes(10);
			}
			Transaction tx = new Transaction();
			tx.Version = 2;
			for(int i = 0 ; i < sequences.Length ; i++)
			{
				TxIn input = new TxIn();
				input.Sequence = sequences[i];
				tx.Inputs.Add(input);
			}
			Assert.Equal(expected, tx.CheckSequenceLocks(prevHeights, chain.Tip));
			var actualLock = tx.CalculateSequenceLocks(prevHeights, chain.Tip);
			Assert.Equal(expectedLock.MinTime, actualLock.MinTime);
			Assert.Equal(expectedLock.MinHeight, actualLock.MinHeight);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanEstimateFees()
		{
			var alice = new Key();
			var bob = new Key();
			var satoshi = new Key();
			var bobAlice = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, alice.PubKey, bob.PubKey);

			//Alice sends money to bobAlice
			//Bob sends money to bobAlice
			//bobAlice sends money to satoshi

			var aliceCoins = new ICoin[] { RandomCoin("0.4", alice), RandomCoin("0.6", alice) };
			var bobCoins = new ICoin[] { RandomCoin("0.2", bob), RandomCoin("0.3", bob) };
			var bobAliceCoins = new ICoin[] { RandomCoin("1.5", bobAlice, false), RandomCoin("0.25", bobAlice, true) };

			TransactionBuilder builder = new TransactionBuilder();
			builder.StandardTransactionPolicy = EasyPolicy;
			var unsigned = builder
				.AddCoins(aliceCoins)
				.Send(bobAlice, "1.0")
				.Then()
				.AddCoins(bobCoins)
				.Send(bobAlice, "0.5")
				.Then()
				.AddCoins(bobAliceCoins)
				.Send(satoshi.PubKey, "1.74")
				.SetChange(bobAlice)
				.BuildTransaction(false);

			builder.AddKeys(alice, bob, satoshi);
			var signed = builder.BuildTransaction(true);
			Assert.True(builder.Verify(signed));

			Assert.True(Math.Abs(signed.ToBytes().Length - builder.EstimateSize(unsigned)) < 20);

			var rate = new FeeRate(Money.Coins(0.0004m));
			var estimatedFees = builder.EstimateFees(unsigned, rate);
			builder.SendEstimatedFees(rate);
			signed = builder.BuildTransaction(true);
			Assert.True(builder.Verify(signed, estimatedFees));
		}

		private Coin RandomCoin(Money amount, Script scriptPubKey, bool p2sh)
		{
			var outpoint = RandOutpoint();
			if(!p2sh)
				return new Coin(outpoint, new TxOut(amount, scriptPubKey));
			return new ScriptCoin(outpoint, new TxOut(amount, scriptPubKey.Hash), scriptPubKey);
		}
		private Coin RandomCoin(Money amount, Key receiver)
		{
			return RandomCoin(amount, receiver.PubKey.GetAddress(Network.Main));
		}
		private Coin RandomCoin(Money amount, BitcoinAddress receiver)
		{
			var outpoint = RandOutpoint();
			return new Coin(outpoint, new TxOut(amount, receiver));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanSerializeInvalidTransactionsBackAndForth()
		{
			Transaction before = new Transaction();
			var versionBefore = before.Version;
			before.Outputs.Add(new TxOut());
			Transaction after = AssertClone(before);
			Assert.Equal(before.Version, after.Version);
			Assert.Equal(versionBefore, after.Version);
			Assert.True(after.Outputs.Count == 1);

			before = new Transaction();
			after = AssertClone(before);
			Assert.Equal(before.Version, versionBefore);
		}

		private Transaction AssertClone(Transaction before)
		{
			Transaction after = before.Clone();
			Transaction after2 = null;

			MemoryStream ms = new MemoryStream();
			BitcoinStream stream = new BitcoinStream(ms, true);
			stream.TransactionOptions = TransactionOptions.None;
			stream.ReadWrite(before);

			ms.Position = 0;

			stream = new BitcoinStream(ms, false);
			stream.TransactionOptions = TransactionOptions.Witness;
			stream.ReadWrite(ref after2);

			Assert.Equal(after2.GetHash(), after.GetHash());
			Assert.Equal(before.GetHash(), after.GetHash());

			return after;
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanBuildWitTransaction()
		{
			Key alice = new Key();
			Key bob = new Key();
			Transaction previousTx = null;
			Coin previousCoin = null;
			WitScriptCoin witnessCoin = null;
			TransactionBuilder builder = null;
			Transaction signedTx = null;
			ScriptCoin scriptCoin = null;

			//P2WPKH
			previousTx = new Transaction();
			previousTx.Outputs.Add(new TxOut(Money.Coins(1.0m), alice.PubKey.WitHash));
			previousCoin = previousTx.Outputs.AsCoins().First();

			builder = new TransactionBuilder();
			builder.AddKeys(alice);
			builder.AddCoins(previousCoin);
			builder.Send(bob, Money.Coins(0.4m));
			builder.SendFees(Money.Satoshis(30000));
			builder.SetChange(alice);
			signedTx = builder.BuildTransaction(true);
			Assert.True(builder.Verify(signedTx));

			//P2WSH
			previousTx = new Transaction();
			previousTx.Outputs.Add(new TxOut(Money.Coins(1.0m), alice.PubKey.ScriptPubKey.WitHash));
			previousCoin = previousTx.Outputs.AsCoins().First();

			witnessCoin = new WitScriptCoin(previousCoin, alice.PubKey.ScriptPubKey);
			builder = new TransactionBuilder();
			builder.AddKeys(alice);
			builder.AddCoins(witnessCoin);
			builder.Send(bob, Money.Coins(0.4m));
			builder.SendFees(Money.Satoshis(30000));
			builder.SetChange(alice);
			signedTx = builder.BuildTransaction(true);
			Assert.True(builder.Verify(signedTx));


			//P2SH(P2WPKH)
			previousTx = new Transaction();
			previousTx.Outputs.Add(new TxOut(Money.Coins(1.0m), alice.PubKey.WitHash.ScriptPubKey.Hash));
			previousCoin = previousTx.Outputs.AsCoins().First();

			scriptCoin = new ScriptCoin(previousCoin, alice.PubKey.WitHash.ScriptPubKey);
			builder = new TransactionBuilder();
			builder.AddKeys(alice);
			builder.AddCoins(scriptCoin);
			builder.Send(bob, Money.Coins(0.4m));
			builder.SendFees(Money.Satoshis(30000));
			builder.SetChange(alice);
			signedTx = builder.BuildTransaction(true);
			Assert.True(builder.Verify(signedTx));

			//P2SH(P2WSH)
			previousTx = new Transaction();
			previousTx.Outputs.Add(new TxOut(Money.Coins(1.0m), alice.PubKey.ScriptPubKey.WitHash.ScriptPubKey.Hash));
			previousCoin = previousTx.Outputs.AsCoins().First();

			witnessCoin = new WitScriptCoin(previousCoin, alice.PubKey.ScriptPubKey);
			builder = new TransactionBuilder();
			builder.AddKeys(alice);
			builder.AddCoins(witnessCoin);
			builder.Send(bob, Money.Coins(0.4m));
			builder.SendFees(Money.Satoshis(30000));
			builder.SetChange(alice);
			signedTx = builder.BuildTransaction(true);
			Assert.True(builder.Verify(signedTx));

			//Can remove witness data from tx
			var signedTx2 = signedTx.WithOptions(TransactionOptions.None);
			Assert.Equal(signedTx.GetHash(), signedTx2.GetHash());
			Assert.True(signedTx2.GetSerializedSize() < signedTx.GetSerializedSize());
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanCheckSegwitPubkey()
		{
			var a = new Script("OP_DUP 033fbe0a2aa8dc28ee3b2e271e3fedc7568529ffa20df179b803bf9073c11b6a8b OP_CHECKSIG OP_IF OP_DROP 0382fdfb0a3898bc6504f63204e7d15a63be82a3b910b5b865690dc96d1249f98c OP_ELSE OP_CODESEPARATOR 033fbe0a2aa8dc28ee3b2e271e3fedc7568529ffa20df179b803bf9073c11b6a8b OP_ENDIF OP_CHECKSIG");
			Assert.False(PayToWitTemplate.Instance.CheckScriptPubKey(a));
			a = new Script("1 033fbe0a2aa8dc28ee3b2e271e3fedc7568529ffa20df179b803bf9073c11b6a8b");
			Assert.True(PayToWitTemplate.Instance.CheckScriptPubKey(a));

			foreach(int pushSize in new[] { 2, 10, 20, 30, 50, 100, 200 })
			{
				a = new Script("1 " + String.Concat(Enumerable.Range(0, pushSize * 2).Select(_ => "0").ToArray()));
				Assert.True(PayToWitTemplate.Instance.CheckScriptPubKey(a));
			}
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanEstimatedFeesCorrectlyIfFeesChangeTransactionSize()
		{
			var redeem = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, new Key().PubKey, new Key().PubKey, new Key().PubKey);
			var transactionBuilder = new TransactionBuilder();
			transactionBuilder.AddCoins(new Coin(new OutPoint(uint256.Parse("75425c904289f21feef0cffab2081ba22030b633623115adf0780edad443e6c7"), 1), new TxOut("0.00010000", PayToScriptHashTemplate.Instance.GenerateScriptPubKey(redeem).GetDestinationAddress(Network.Main))).ToScriptCoin(redeem));
			transactionBuilder.AddCoins(new Coin(new OutPoint(uint256.Parse("75425c904289f21feef0cffab2081ba22030b633623115adf0780edad443e6c7"), 2), new TxOut("0.00091824", PayToScriptHashTemplate.Instance.GenerateScriptPubKey(redeem).GetDestinationAddress(Network.Main))).ToScriptCoin(redeem));
			transactionBuilder.AddCoins(new Coin(new OutPoint(uint256.Parse("75425c904289f21feef0cffab2081ba22030b633623115adf0780edad443e6c7"), 3), new TxOut("0.00100000", PayToScriptHashTemplate.Instance.GenerateScriptPubKey(redeem).GetDestinationAddress(Network.Main))).ToScriptCoin(redeem));
			transactionBuilder.AddCoins(new Coin(new OutPoint(uint256.Parse("75425c904289f21feef0cffab2081ba22030b633623115adf0780edad443e6c7"), 4), new TxOut("0.00100000", PayToScriptHashTemplate.Instance.GenerateScriptPubKey(redeem).GetDestinationAddress(Network.Main))).ToScriptCoin(redeem));
			transactionBuilder.AddCoins(new Coin(new OutPoint(uint256.Parse("75425c904289f21feef0cffab2081ba22030b633623115adf0780edad443e6c7"), 5), new TxOut("0.00246414", PayToScriptHashTemplate.Instance.GenerateScriptPubKey(redeem).GetDestinationAddress(Network.Main))).ToScriptCoin(redeem));
			transactionBuilder.AddCoins(new Coin(new OutPoint(uint256.Parse("75425c904289f21feef0cffab2081ba22030b633623115adf0780edad443e6c7"), 6), new TxOut("0.00250980", PayToScriptHashTemplate.Instance.GenerateScriptPubKey(redeem).GetDestinationAddress(Network.Main))).ToScriptCoin(redeem));
			transactionBuilder.AddCoins(new Coin(new OutPoint(uint256.Parse("75425c904289f21feef0cffab2081ba22030b633623115adf0780edad443e6c7"), 7), new TxOut("0.01000000", PayToScriptHashTemplate.Instance.GenerateScriptPubKey(redeem).GetDestinationAddress(Network.Main))).ToScriptCoin(redeem));
			transactionBuilder.Send(new Key().PubKey.GetAddress(Network.Main), "0.01000000");
			transactionBuilder.SetChange(new Key().PubKey.GetAddress(Network.Main));

			var feeRate = new FeeRate((long)32563);
			//Adding the estimated fees will cause 6 more coins to be included, so let's verify the actual sent fees take that into account
			transactionBuilder.SendEstimatedFees(feeRate);
			var tx = transactionBuilder.BuildTransaction(false);
			var estimation = transactionBuilder.EstimateFees(tx, feeRate);
			Assert.Equal(estimation, tx.GetFee(transactionBuilder.FindSpentCoins(tx)));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanBuildTransaction()
		{
			var keys = Enumerable.Range(0, 5).Select(i => new Key()).ToArray();

			var multiSigPubKey = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, keys.Select(k => k.PubKey).Take(3).ToArray());
			var pubKeyPubKey = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(keys[4].PubKey);
			var pubKeyHashPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(keys[4].PubKey.Hash);
			var scriptHashPubKey1 = PayToScriptHashTemplate.Instance.GenerateScriptPubKey(multiSigPubKey.Hash);
			var scriptHashPubKey2 = PayToScriptHashTemplate.Instance.GenerateScriptPubKey(pubKeyPubKey.Hash);
			var scriptHashPubKey3 = PayToScriptHashTemplate.Instance.GenerateScriptPubKey(pubKeyHashPubKey.Hash);


			var coins = new[] { multiSigPubKey, pubKeyPubKey, pubKeyHashPubKey }.Select((script, i) =>
				new Coin
					(
					new OutPoint(Rand(), i),
					new TxOut(new Money((i + 1) * Money.COIN), script)
					)).ToList();

			var scriptCoins =
				new[] { scriptHashPubKey1, scriptHashPubKey2, scriptHashPubKey3 }
				.Zip(new[] { multiSigPubKey, pubKeyPubKey, pubKeyHashPubKey },
					(script, redeem) => new
					{
						script,
						redeem
					})
				.Select((_, i) =>
				new ScriptCoin
					(
					new OutPoint(Rand(), i),
					new TxOut(new Money((i + 1) * Money.COIN), _.script), _.redeem
					)).ToList();

			var witCoins =
			new[] { scriptHashPubKey1, scriptHashPubKey2, scriptHashPubKey3 }
			.Zip(new[] { multiSigPubKey, pubKeyPubKey, pubKeyHashPubKey },
				(script, redeem) => new
				{
					script,
					redeem
				})
			.Select((_, i) =>
			new WitScriptCoin
				(
				new OutPoint(Rand(), i),
				new TxOut(new Money((i + 1) * Money.COIN), _.redeem.WitHash.ScriptPubKey.Hash),
				_.redeem
				)).ToList();
			var a = witCoins.Select(c => c.Amount).Sum();
			var allCoins = coins.Concat(scriptCoins).Concat(witCoins).ToArray();
			var destinations = keys.Select(k => k.PubKey.GetAddress(Network.Main)).ToArray();

			var txBuilder = new TransactionBuilder(0);
			txBuilder.StandardTransactionPolicy = EasyPolicy;
			var tx = txBuilder
				.AddCoins(allCoins)
				.AddKeys(keys)
				.Send(destinations[0], Money.Parse("6") * 2)
				.Send(destinations[2], Money.Parse("5"))
				.Send(destinations[2], Money.Parse("0.9999"))
				.SendFees(Money.Parse("0.0001"))
				.SetChange(destinations[3])
				.BuildTransaction(true);
			Assert.True(txBuilder.Verify(tx, "0.0001"));

			Assert.Equal(3, tx.Outputs.Count);

			txBuilder = new TransactionBuilder(0);
			txBuilder.StandardTransactionPolicy = EasyPolicy;
			tx = txBuilder
			   .AddCoins(allCoins)
			   .AddKeys(keys)
			   .SetGroupName("test")
			   .Send(destinations[0], Money.Parse("6") * 2)
			   .Send(destinations[2], Money.Parse("5"))
			   .Send(destinations[2], Money.Parse("0.9998"))
			   .SendFees(Money.Parse("0.0001"))
			   .SetChange(destinations[3])
			   .BuildTransaction(true);

			Assert.Equal(4, tx.Outputs.Count); //+ Change

			txBuilder.Send(destinations[4], Money.Parse("1"));
			var ex = Assert.Throws<NotEnoughFundsException>(() => txBuilder.BuildTransaction(true));
			Assert.True(ex.Group == "test");
			Assert.True((Money)ex.Missing == Money.Parse("0.9999"));
			//Can sign partially
			txBuilder = new TransactionBuilder(0);
			txBuilder.StandardTransactionPolicy = EasyPolicy;
			tx = txBuilder
					.AddCoins(allCoins)
					.AddKeys(keys.Skip(2).ToArray())  //One of the multi key missing
					.Send(destinations[0], Money.Parse("6") * 2)
					.Send(destinations[2], Money.Parse("5"))
					.Send(destinations[2], Money.Parse("0.9998"))
					.SendFees(Money.Parse("0.0001"))
					.SetChange(destinations[3])
					.Shuffle()
					.BuildTransaction(true);
			Assert.False(txBuilder.Verify(tx, "0.0001"));

			txBuilder = new TransactionBuilder(0);
			tx = txBuilder
					.AddKeys(keys[0])
					.AddCoins(allCoins)
					.SignTransaction(tx);

			Assert.True(txBuilder.Verify(tx));

			//Test if signing separatly
			txBuilder = new TransactionBuilder(0);
			txBuilder.StandardTransactionPolicy = EasyPolicy;
			tx = txBuilder
					.AddCoins(allCoins)
					.AddKeys(keys.Skip(2).ToArray())  //One of the multi key missing
					.Send(destinations[0], Money.Parse("6") * 2)
					.Send(destinations[2], Money.Parse("5"))
					.Send(destinations[2], Money.Parse("0.9998"))
					.SendFees(Money.Parse("0.0001"))
					.SetChange(destinations[3])
					.Shuffle()
					.BuildTransaction(false);

			var signed1 = txBuilder.SignTransaction(tx);

			txBuilder = new TransactionBuilder(0);
			var signed2 = txBuilder
					.AddKeys(keys[0])
					.AddCoins(allCoins)
					.SignTransaction(tx);

			Assert.False(txBuilder.Verify(signed1));
			Assert.False(txBuilder.Verify(signed2));

			txBuilder = new TransactionBuilder(0);
			txBuilder.StandardTransactionPolicy = EasyPolicy;
			tx = txBuilder
				.AddCoins(allCoins)
				.CombineSignatures(signed1, signed2);
			Assert.True(txBuilder.Verify(tx));

			//Check if can deduce scriptPubKey from P2SH and P2SPKH scriptSig
			allCoins = new[]
				{ 
					RandomCoin(Money.Parse("1.0"), keys[0].PubKey.Hash.ScriptPubKey, false),
					RandomCoin(Money.Parse("1.0"), keys[0].PubKey.Hash.ScriptPubKey, false),
					RandomCoin(Money.Parse("1.0"), keys[1].PubKey.Hash.ScriptPubKey, false)
				};

			txBuilder = new TransactionBuilder(0);
			txBuilder.StandardTransactionPolicy = EasyPolicy;
			tx =
				txBuilder.AddCoins(allCoins)
					 .Send(destinations[0], Money.Parse("3.0"))
					 .BuildTransaction(false);

			signed1 = new TransactionBuilder(0)
						.AddCoins(allCoins)
						.AddKeys(keys[0])
						.SignTransaction(tx);

			signed2 = new TransactionBuilder(0)
						.AddCoins(allCoins)
						.AddKeys(keys[1])
						.SignTransaction(tx);

			Assert.False(txBuilder.Verify(signed1));
			Assert.False(txBuilder.Verify(signed2));

			tx = new TransactionBuilder(0)
				.CombineSignatures(signed1, signed2);

			Assert.True(txBuilder.Verify(tx));

			//Using the same set of coin in 2 group should not use two times the sames coins
			for(int i = 0 ; i < 3 ; i++)
			{
				txBuilder = new TransactionBuilder();
				txBuilder.StandardTransactionPolicy = EasyPolicy;
				tx =
					txBuilder
					.AddCoins(allCoins)
					.AddKeys(keys)
					.Send(destinations[0], Money.Parse("2.0"))
					.Then()
					.AddCoins(allCoins)
					.AddKeys(keys)
					.Send(destinations[0], Money.Parse("1.0"))
					.BuildTransaction(true);
				Assert.True(txBuilder.Verify(tx));
			}
		}

		private uint256 Rand()
		{
			return new uint256(RandomUtils.GetBytes(32));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		//https://gist.github.com/gavinandresen/3966071
		public void CanBuildTransactionWithDustPrevention()
		{
			var bob = new Key();
			var alice = new Key();
			var tx = new Transaction()
			{
				Outputs =
				{
					new TxOut(Money.Coins(1.0m), bob)
				}
			};
			var coins = tx.Outputs.AsCoins().ToArray();

			var builder = new TransactionBuilder();
			builder.StandardTransactionPolicy = EasyPolicy.Clone();
			builder.StandardTransactionPolicy.MinRelayTxFee = new FeeRate(new Money(1000));

			Func<Transaction> create = () => builder
				.AddCoins(coins)
				.AddKeys(bob)
				.Send(alice, Money.Coins(0.99m))
				.Send(alice, Money.Satoshis(500))
				.Send(TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 1, 2 }), Money.Zero)
				.SendFees(Money.Coins(0.0001m))
				.SetChange(bob)
				.BuildTransaction(true);

			var signed = create();

			Assert.True(signed.Outputs.Count == 3);
			Assert.True(builder.Verify(signed, Money.Coins(0.0001m)));
			builder.DustPrevention = false;

			TransactionPolicyError[] errors;
			Assert.False(builder.Verify(signed, Money.Coins(0.0001m), out errors));
			var ex = (NotEnoughFundsPolicyError)errors.Single();
			Assert.True((Money)ex.Missing == Money.Parse("-0.00000500"));

			builder = new TransactionBuilder();
			builder.DustPrevention = false;
			builder.StandardTransactionPolicy = EasyPolicy.Clone();
			builder.StandardTransactionPolicy.MinRelayTxFee = new FeeRate(new Money(1000));
			signed = create();
			Assert.True(signed.Outputs.Count == 4);
			Assert.False(builder.Verify(signed, out errors));
			Assert.True(errors.Length == 1);
			Assert.True(errors[0] is DustPolicyError);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		//https://gist.github.com/gavinandresen/3966071
		public void CanPartiallySignTransaction()
		{
			var privKeys = new[]{"5JaTXbAUmfPYZFRwrYaALK48fN6sFJp4rHqq2QSXs8ucfpE4yQU",
						"5Jb7fCeh1Wtm4yBBg3q3XbT6B525i17kVhy3vMC9AqfR6FH2qGk",
						"5JFjmGo5Fww9p8gvx48qBYDJNAzR9pmH5S389axMtDyPT8ddqmw"}
						.Select(k => new BitcoinSecret(k).PrivateKey).ToArray();

			//First: combine the three keys into a multisig address
			var redeem = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, privKeys.Select(k => k.PubKey).ToArray());
			var scriptAddress = redeem.Hash.GetAddress(Network.Main);
			Assert.Equal("3QJmV3qfvL9SuYo34YihAf3sRCW3qSinyC", scriptAddress.ToString());

			// Next, create a transaction to send funds into that multisig. Transaction d6f72... is
			// an unspent transaction in my wallet (which I got from the 'listunspent' RPC call):
			// Taken from example
			var fundingTransaction = Transaction.Parse("010000000189632848f99722915727c5c75da8db2dbf194342a0429828f66ff88fab2af7d6000000008b483045022100abbc8a73fe2054480bda3f3281da2d0c51e2841391abd4c09f4f908a2034c18d02205bc9e4d68eafb918f3e9662338647a4419c0de1a650ab8983f1d216e2a31d8e30141046f55d7adeff6011c7eac294fe540c57830be80e9355c83869c9260a4b8bf4767a66bacbd70b804dc63d5beeb14180292ad7f3b083372b1d02d7a37dd97ff5c9effffffff0140420f000000000017a914f815b036d9bbbce5e9f2a00abd1bf3dc91e955108700000000");

			// Create the spend-from-multisig transaction. Since the fund-the-multisig transaction
			// hasn't been sent yet, I need to give txid, scriptPubKey and redeemScript:
			var spendTransaction = new Transaction();
			spendTransaction.Inputs.Add(new TxIn()
			{
				PrevOut = new OutPoint(fundingTransaction.GetHash(), 0),
			});
			spendTransaction.Outputs.Add(new TxOut()
			{
				Value = "0.01000000",
				ScriptPubKey = new Script("OP_DUP OP_HASH160 ae56b4db13554d321c402db3961187aed1bbed5b OP_EQUALVERIFY OP_CHECKSIG")
			});

			spendTransaction.Inputs[0].ScriptSig = redeem; //The redeem should be in the scriptSig before signing

			var partiallySigned = spendTransaction.Clone();
			//... Now I can partially sign it using one private key:

			partiallySigned.Sign(privKeys[0], true);

			//the other private keys (note the "hex" result getting longer):
			partiallySigned.Sign(privKeys[1], true);


			AssertCorrectlySigned(partiallySigned, fundingTransaction.Outputs[0].ScriptPubKey, allowHighS);

			//Verify the transaction from the gist is also correctly signed
			var gistTransaction = Transaction.Parse("0100000001aca7f3b45654c230e0886a57fb988c3044ef5e8f7f39726d305c61d5e818903c00000000fd5d010048304502200187af928e9d155c4b1ac9c1c9118153239aba76774f775d7c1f9c3e106ff33c0221008822b0f658edec22274d0b6ae9de10ebf2da06b1bbdaaba4e50eb078f39e3d78014730440220795f0f4f5941a77ae032ecb9e33753788d7eb5cb0c78d805575d6b00a1d9bfed02203e1f4ad9332d1416ae01e27038e945bc9db59c732728a383a6f1ed2fb99da7a4014cc952410491bba2510912a5bd37da1fb5b1673010e43d2c6d812c514e91bfa9f2eb129e1c183329db55bd868e209aac2fbc02cb33d98fe74bf23f0c235d6126b1d8334f864104865c40293a680cb9c020e7b1e106d8c1916d3cef99aa431a56d253e69256dac09ef122b1a986818a7cb624532f062c1d1f8722084861c5c3291ccffef4ec687441048d2455d2403e08708fc1f556002f1b6cd83f992d085097f9974ab08a28838f07896fbab08f39495e15fa6fad6edbfb1e754e35fa1c7844c41f322a1863d4621353aeffffffff0140420f00000000001976a914ae56b4db13554d321c402db3961187aed1bbed5b88ac00000000");

			AssertCorrectlySigned(gistTransaction, fundingTransaction.Outputs[0].ScriptPubKey, allowHighS); //One sig in the hard code tx is high

			//Can sign out of order
			partiallySigned = spendTransaction.Clone();
			partiallySigned.Sign(privKeys[2], true);
			partiallySigned.Sign(privKeys[0], true);
			AssertCorrectlySigned(partiallySigned, fundingTransaction.Outputs[0].ScriptPubKey);

			//Can sign multiple inputs
			partiallySigned = spendTransaction.Clone();
			partiallySigned.Inputs.Add(new TxIn()
			{
				PrevOut = new OutPoint(fundingTransaction.GetHash(), 1),
			});
			partiallySigned.Inputs[1].ScriptSig = redeem; //The redeem should be in the scriptSig before signing
			partiallySigned.Sign(privKeys[2], true);
			partiallySigned.Sign(privKeys[0], true);
		}

		private void AssertCorrectlySigned(Transaction tx, Script scriptPubKey, ScriptVerify scriptVerify = ScriptVerify.Standard)
		{
			for(int i = 0 ; i < tx.Inputs.Count ; i++)
			{
				Assert.True(Script.VerifyScript(scriptPubKey, tx, i, null, scriptVerify));
			}
		}

		static StandardTransactionPolicy EasyPolicy = new StandardTransactionPolicy()
		{
			MaxTransactionSize = null,
			MaxTxFee = null,
			MinRelayTxFee = null,
			ScriptVerify = ScriptVerify.Standard & ~ScriptVerify.LowS
		};

		static StandardTransactionPolicy RelayPolicy = new StandardTransactionPolicy()
		{
			MaxTransactionSize = null,
			MaxTxFee = null,
			MinRelayTxFee = new FeeRate(Money.Satoshis(5000)),
			ScriptVerify = ScriptVerify.Standard & ~ScriptVerify.LowS
		};

		[Trait("UnitTest", "UnitTest")]
		[Fact]
		public void CanMutateSignature()
		{
			Transaction funding = new Transaction("010000000189632848f99722915727c5c75da8db2dbf194342a0429828f66ff88fab2af7d6000000008b483045022100abbc8a73fe2054480bda3f3281da2d0c51e2841391abd4c09f4f908a2034c18d02205bc9e4d68eafb918f3e9662338647a4419c0de1a650ab8983f1d216e2a31d8e30141046f55d7adeff6011c7eac294fe540c57830be80e9355c83869c9260a4b8bf4767a66bacbd70b804dc63d5beeb14180292ad7f3b083372b1d02d7a37dd97ff5c9effffffff0140420f000000000017a914f815b036d9bbbce5e9f2a00abd1bf3dc91e955108700000000");

			Transaction spending = new Transaction("0100000001aca7f3b45654c230e0886a57fb988c3044ef5e8f7f39726d305c61d5e818903c00000000fd5d010048304502200187af928e9d155c4b1ac9c1c9118153239aba76774f775d7c1f9c3e106ff33c0221008822b0f658edec22274d0b6ae9de10ebf2da06b1bbdaaba4e50eb078f39e3d78014730440220795f0f4f5941a77ae032ecb9e33753788d7eb5cb0c78d805575d6b00a1d9bfed02203e1f4ad9332d1416ae01e27038e945bc9db59c732728a383a6f1ed2fb99da7a4014cc952410491bba2510912a5bd37da1fb5b1673010e43d2c6d812c514e91bfa9f2eb129e1c183329db55bd868e209aac2fbc02cb33d98fe74bf23f0c235d6126b1d8334f864104865c40293a680cb9c020e7b1e106d8c1916d3cef99aa431a56d253e69256dac09ef122b1a986818a7cb624532f062c1d1f8722084861c5c3291ccffef4ec687441048d2455d2403e08708fc1f556002f1b6cd83f992d085097f9974ab08a28838f07896fbab08f39495e15fa6fad6edbfb1e754e35fa1c7844c41f322a1863d4621353aeffffffff0140420f00000000001976a914ae56b4db13554d321c402db3961187aed1bbed5b88ac00000000");


			TransactionBuilder builder = new TransactionBuilder();
			builder.StandardTransactionPolicy = EasyPolicy;
			builder.AddCoins(funding.Outputs.AsCoins());
			Assert.True(builder.Verify(spending));

			foreach(var input in spending.Inputs.AsIndexedInputs())
			{
				var ops = input.TxIn.ScriptSig.ToOps().ToArray();
				foreach(var sig in ops.Select(o =>
					{
						try
						{
							return new TransactionSignature(o.PushData);
						}
						catch
						{
							return null;
						}
					})
					.Select((sig, i) => new
					{
						sig,
						i
					})
					.Where(i => i.sig != null))
				{
					ops[sig.i] = Op.GetPushOp(sig.sig.MakeCanonical().ToBytes());
				}
				input.TxIn.ScriptSig = new Script(ops);
			}
			Assert.True(builder.Verify(spending));
		}
		ScriptVerify allowHighS = ScriptVerify.Standard & ~ScriptVerify.LowS;
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanUseLockTime()
		{
			var tx = new Transaction();
			tx.LockTime = new LockTime(4);
			var clone = tx.Clone();
			Assert.Equal(tx.LockTime, clone.LockTime);

			Assert.Equal("Height : 0", new LockTime().ToString());
			Assert.Equal(3, (int)new LockTime(3));
			Assert.Equal((uint)3, (uint)new LockTime(3));
			Assert.Throws<InvalidOperationException>(() => (DateTimeOffset)new LockTime(3));

			var now = DateTimeOffset.UtcNow;
			Assert.Equal("Date : " + now, new LockTime(now).ToString());
			Assert.Equal((int)Utils.DateTimeToUnixTime(now), (int)new LockTime(now));
			Assert.Equal(Utils.DateTimeToUnixTime(now), (uint)new LockTime(now));
			Assert.Equal(now.ToString(), ((DateTimeOffset)new LockTime(now)).ToString());
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		//http://brainwallet.org/#tx
		public void CanGetTransactionErrors()
		{
			Key bob = new Key();
			Key alice = new Key();

			var funding = new Transaction();
			funding.Outputs.Add(new TxOut(Money.Coins(1.0m), bob));
			funding.Outputs.Add(new TxOut(Money.Coins(1.1m), bob));
			funding.Outputs.Add(new TxOut(Money.Coins(1.2m), alice));

			var spending = new Transaction();
			spending.Inputs.Add(new TxIn(new OutPoint(funding, 0)));
			spending.Inputs.Add(new TxIn(new OutPoint(funding, 0))); //Duplicate
			spending.Inputs.Add(new TxIn(new OutPoint(funding, 1)));
			spending.Inputs.Add(new TxIn(new OutPoint(funding, 2))); //Alice will not sign

			spending.Outputs.Add(new TxOut(Money.Coins(4.0m), bob));


			TransactionPolicyError[] errors = null;
			TransactionBuilder builder = new TransactionBuilder();
			builder.StandardTransactionPolicy = EasyPolicy;
			builder.AddKeys(bob);
			builder.AddCoins(funding.Outputs.AsCoins());
			builder.SignTransactionInPlace(spending);
			Assert.False(builder.Verify(spending, Money.Coins(1.0m), out errors));

			var dup = errors.OfType<DuplicateInputPolicyError>().Single();
			AssertEx.CollectionEquals(new uint[] { 0, 1 }, dup.InputIndices);
			AssertEx.Equals(new OutPoint(funding, 0), dup.OutPoint);

			var script = errors.OfType<ScriptPolicyError>().Single();
			AssertEx.Equals(alice.ScriptPubKey, script.ScriptPubKey);
			AssertEx.Equals(3, script.InputIndex);

			var fees = errors.OfType<NotEnoughFundsPolicyError>().Single();
			Assert.Equal(fees.Missing, Money.Coins(0.7m));

			spending.Inputs.Add(new TxIn(new OutPoint(funding, 3))); //Coins not found
			builder.Verify(spending, Money.Coins(1.0m), out errors);
			var coin = errors.OfType<CoinNotFoundPolicyError>().Single();
			Assert.Equal(coin.InputIndex, 4UL);
			Assert.Equal(coin.OutPoint.N, 3UL);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanCheckSegwitSig()
		{
			Transaction tx = new Transaction("010000000001015d896079097272b13ed9cb22acfabeca9ce83f586d98cc15a08ea2f9c558013b0300000000ffffffff01605af40500000000160014a8cbb5eca9af499cecaa08457690ab367f23d95b0247304402200b6baba4287f3321ae4ec6ba66420d9a48c3f3bc331603e7dca6b12ca75cce6102207fa582041b025605c0474b99a2d3ab5080d6ea14ae3a50b7de92596abf40fb4b012102cdfc0f4701e0c8db3a0913de5f635d0ea76663a8f80925567358d558603fae3500000000");
			CanCheckSegwitSigCore(tx, 0, Money.Coins(1.0m));

			Transaction toCheck = new Transaction("01000000000103b019e2344634c5b34aeb867f2cd8b09dbbd95b5bf8c5d56d58be1dd9077f9d3a00000000da0047304402201b2be1016abd4df4ca699e0430b97bc8dcd4c1c90b6a6ee382be75f42956566402205ab38fddace15ba4b2c4dbacc6793bb1f35a371aa8386f1348bd65dfeda9657201483045022100db1dbea1a5d05ff7daf6d106931ab701a29d2dddd8cd7781e9eb7fefd31139790220319eb8a238e6c635ebe2960f5960eeb96371f5a38503cf41aa89a33807c8b6a50147522102a96e9843b846b8cc3277ea54638f1454378219854ef89c81a8a4e9217f1f3ca02103d5feb2e2f2fa1403ede18aaac7631dd2c9a893953a9ab338e7d9fa749d91f03b52aeffffffffb019e2344634c5b34aeb867f2cd8b09dbbd95b5bf8c5d56d58be1dd9077f9d3a01000000db00483045022100aec68f5760337efdf425007387f094df284a576e824492597b0d046e038034100220434cb22f056e97cd823a13751c482a9f2d3fb956abcfa69db4dcd2679379070101483045022100c7ce0a9617cbcaa9308758092d336b228f67d358ad25a786711a87a29e2f72d102203d608bf6a4416e9493a5d89552633da300e9a237811e9affea3cda3320a3257c0147522102c4bd91a554815c73814848b311051c43ad6a75810269e1ff0eb9c13d828fc6fb21031035e69a48e04bc4d6315590620f784ab79d8369d122bd45ad7e77c81ac1cb1c52aeffffffffbcf750fad5ddd1909d8b3e2edda94f7ae3c866952932823763291b9467e3b9580000000023220020e0be53749d09a8e2d3843633cf11133e51e73944334d11a147f1ae53f1c3dfe5ffffffff019cbaf0080000000017a9148d52e4999751ec43c07eb371119f8c45047d26dc870000040047304402205bdc03fac6c3be92309e4fdd1572147ca56210dbb4413539874a4e3b0670ac0b02206422cd069e6078bcdc8f698ff77aed65566b6fa1ff028cc322d14d036d2c192401473044022022fa0bda2e8e21716b9d74499665e4f31cbcf2bf49d0b535188e7e196e8e90d8022076ad55655fbd54637c0cf5bbd7f07905446e23a621f82a940cb07677dab2f8fe0147522102d01cf4abc1b6c22cc0e0e43e5277f1a7fb544eca52244cd4cb88bef5943c5563210284a2ffb3e6b6ac0ac9444b0ecd9856f79b53bbd3100894ec6dc80e6e956edbeb52ae00000000");

			ScriptError error;
			Assert.True(toCheck.Inputs.AsIndexedInputs().Skip(0).First().VerifyScript(new Script("OP_HASH160 442afa4f034468652c571202da0bf277cb729def OP_EQUAL"), Money.Satoshis(100000), ScriptVerify.Mandatory, out error));
		}

		private static void CanCheckSegwitSigCore(Transaction tx, int input, Money amount, string scriptCodeHex = null)
		{
			Script scriptCode = null;
			if(scriptCodeHex == null)
			{
				var param1 = PayToWitPubKeyHashTemplate.Instance.ExtractWitScriptParameters(tx.Inputs[input].WitScript);
				Assert.NotNull(param1);
				var param2 = PayToWitPubKeyHashTemplate.Instance.ExtractScriptPubKeyParameters(param1.PublicKey.GetSegwitAddress(Network.Main).Hash.ScriptPubKey);
				Assert.Equal(param1.PublicKey.WitHash, param2);
				scriptCode = param1.ScriptPubKey;
			}
			else
			{
				scriptCode = new Script(Encoders.Hex.DecodeData(scriptCodeHex));
			}

			ScriptError err;
			var r = Script.VerifyScript(scriptCode, tx, 0, amount, out err);
			Assert.True(r);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanParseWitTransaction()
		{
			var hex = "010000000001015d896079097272b13ed9cb22acfabeca9ce83f586d98cc15a08ea2f9c558013b0300000000ffffffff01605af40500000000160014a8cbb5eca9af499cecaa08457690ab367f23d95b0247304402200b6baba4287f3321ae4ec6ba66420d9a48c3f3bc331603e7dca6b12ca75cce6102207fa582041b025605c0474b99a2d3ab5080d6ea14ae3a50b7de92596abf40fb4b012102cdfc0f4701e0c8db3a0913de5f635d0ea76663a8f80925567358d558603fae3500000000";
			Transaction tx = new Transaction(hex);
			var bytes = tx.ToBytes();
			Assert.Equal(Encoders.Hex.EncodeData(bytes), hex);

			Assert.Equal("4b3580bbcceb12fee91abc7f9e8e7d092e981d4bb38339204c457a04316d949a", tx.GetHash().ToString());
			Assert.Equal("38331098fb804ef2e6dee7826a74b4af07e631a0f1082ffc063667ccb825d701", tx.GetWitHash().ToString());

			var noWit = tx.WithOptions(TransactionOptions.None);
			Assert.True(noWit.GetSerializedSize() < tx.GetSerializedSize());

			tx = new Transaction("010000000001015d896079097272b13ed9cb22acfabeca9ce83f586d98cc15a08ea2f9c558013b0200000000ffffffff01605af40500000000160014a8cbb5eca9af499cecaa08457690ab367f23d95b02483045022100d3edd272c4ff247c36a1af34a2394859ece319f61ee85f759b94ec0ecd61912402206dbdc7c6ca8f7279405464d2d935b5e171dfd76656872f76399dbf333c0ac3a001fd08020000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000100000000");

			ScriptError error;
			Assert.False(tx.Inputs.AsIndexedInputs().First().VerifyScript(new Script("0 b7854eb547106248b136ca2bf48d8df2f1167588"), out error));
			Assert.Equal(ScriptError.EqualVerify, error);
		}
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void bip142Test()
		{
			Transaction tx = new Transaction("0100000002fff7f7881a8099afa6940d42d1e7f6362bec38171ea3edf433541db4e4ad969f0000000000eeffffffef51e1b804cc89d182d279655c3aa89e815b1b309fe287d9b2b55d57b90ec68a0100000000ffffffff02202cb206000000001976a9148280b37df378db99f66f85c95a783a76ac7a6d5988ac9093510d000000001976a9143bde42dbee7e4dbe6a21b2d50ce2f0167faa815988ac11000000");
			var h = Script.SignatureHash(new Script(Encoders.Hex.DecodeData("76a9141d0f172a0ecb48aee1be1f2687d2963ae33f71a188ac")), tx, 1, SigHash.All, Money.Satoshis(0x23c34600L), HashVersion.Witness);
			Assert.Equal(new uint256(Encoders.Hex.DecodeData("c37af31116d1b27caf68aae9e3ac82f1477929014d5b917657d0eb49478cb670"), true), h);
		}
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void witnessHasPushSizeLimit()
		{
			Key bob = new Key();
			Transaction tx = new Transaction();
			tx.Outputs.Add(new TxOut(Money.Coins(1.0m), bob.PubKey.ScriptPubKey.WitHash));
			WitScriptCoin coin = new WitScriptCoin(tx.Outputs.AsCoins().First(), bob.PubKey.ScriptPubKey);

			Transaction spending = new Transaction();
			spending.AddInput(tx, 0);
			spending.Sign(bob, coin);
			ScriptError error;
			Assert.True(spending.Inputs.AsIndexedInputs().First().VerifyScript(coin, out error));
			spending.Inputs[0].WitScript = new WitScript(new[] { new byte[521] }.Concat(spending.Inputs[0].WitScript.Pushes).ToArray());
			Assert.False(spending.Inputs.AsIndexedInputs().First().VerifyScript(coin, out error));
			Assert.Equal(ScriptError.PushSize, error);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		//http://brainwallet.org/#tx
		public void CanParseTransaction()
		{

			var tests = TestCase.read_json("data/can_parse_transaction.json");

			foreach(var test in tests.Select(t => t.GetDynamic(0)))
			{
				string raw = test.Raw;
				Transaction tx = Transaction.Parse(raw);
				Assert.Equal((int)test.JSON.vin_sz, tx.Inputs.Count);
				Assert.Equal((int)test.JSON.vout_sz, tx.Outputs.Count);
				Assert.Equal((uint)test.JSON.lock_time, (uint)tx.LockTime);

				for(int i = 0 ; i < tx.Inputs.Count ; i++)
				{
					var actualVIn = tx.Inputs[i];
					var expectedVIn = test.JSON.@in[i];
					Assert.Equal(uint256.Parse((string)expectedVIn.prev_out.hash), actualVIn.PrevOut.Hash);
					Assert.Equal((uint)expectedVIn.prev_out.n, actualVIn.PrevOut.N);
					if(expectedVIn.sequence != null)
						Assert.Equal((uint)expectedVIn.sequence, (uint)actualVIn.Sequence);
					Assert.Equal((string)expectedVIn.scriptSig, actualVIn.ScriptSig.ToString());
					//Can parse the string
					Assert.Equal((string)expectedVIn.scriptSig, (string)expectedVIn.scriptSig.ToString());
				}

				for(int i = 0 ; i < tx.Outputs.Count ; i++)
				{
					var actualVOut = tx.Outputs[i];
					var expectedVOut = test.JSON.@out[i];
					Assert.Equal((string)expectedVOut.scriptPubKey, actualVOut.ScriptPubKey.ToString());
					Assert.Equal(Money.Parse((string)expectedVOut.value), actualVOut.Value);
				}
				var hash = (string)test.JSON.hash;
				var expectedHash = new uint256(Encoders.Hex.DecodeData(hash), false);
				Assert.Equal(expectedHash, tx.GetHash());
			}
		}

		//[Fact]
		//http://bitcoin.stackexchange.com/questions/25814/ecdsa-signature-and-the-z-value
		//http://www.nilsschneider.net/2013/01/28/recovering-bitcoin-private-keys.html
		public void PlayingWithSignatures()
		{
			var script1 = new Script("30440220d47ce4c025c35ec440bc81d99834a624875161a26bf56ef7fdc0f5d52f843ad1022044e1ff2dfd8102cf7a47c21d5c9fd5701610d04953c6836596b4fe9dd2f53e3e01 04dbd0c61532279cf72981c3584fc32216e0127699635c2789f549e0730c059b81ae133016a69c21e23f1859a95f06d52b7bf149a8f2fe4e8535c8a829b449c5ff");

			var script2 = new Script("30440220d47ce4c025c35ec440bc81d99834a624875161a26bf56ef7fdc0f5d52f843ad102209a5f1c75e461d7ceb1cf3cab9013eb2dc85b6d0da8c3c6e27e3a5a5b3faa5bab01 04dbd0c61532279cf72981c3584fc32216e0127699635c2789f549e0730c059b81ae133016a69c21e23f1859a95f06d52b7bf149a8f2fe4e8535c8a829b449c5ff");

			var sig1 = (PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(script1).TransactionSignature.Signature);
			var sig2 = (PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(script2).TransactionSignature.Signature);

			var n = ECKey.CURVE.N;
			var z1 = new BigInteger(1, Encoders.Hex.DecodeData("c0e2d0a89a348de88fda08211c70d1d7e52ccef2eb9459911bf977d587784c6e"));
			var z2 = new BigInteger(1, Encoders.Hex.DecodeData("17b0f41c8c337ac1e18c98759e83a8cccbc368dd9d89e5f03cb633c265fd0ddc"));

			var z = z1.Subtract(z2);
			var s = sig1.S.Subtract(sig2.S);
			var n2 = BigInteger.Two.Pow(256).Subtract(new BigInteger("432420386565659656852420866394968145599"));

			var expected = new Key(Encoders.Hex.DecodeData("c477f9f65c22cce20657faa5b2d1d8122336f851a508a1ed04e479c34985bf96"), fCompressedIn: false);

			var expectedBigInt = new NBitcoin.BouncyCastle.Math.BigInteger(1, Encoders.Hex.DecodeData("c477f9f65c22cce20657faa5b2d1d8122336f851a508a1ed04e479c34985bf96"));
			var priv = (z1.Multiply(sig2.S).Subtract(z2.Multiply(sig1.S)).Mod(n)).Divide(sig1.R.Multiply(sig1.S.Subtract(sig2.S)).Mod(n));
			Assert.Equal(expectedBigInt.ToString(), priv.ToString());

		}

		protected virtual BigInteger CalculateE(BigInteger n, byte[] message)
		{
			int messageBitLength = message.Length * 8;
			BigInteger trunc = new BigInteger(1, message);

			if(n.BitLength < messageBitLength)
			{
				trunc = trunc.ShiftRight(messageBitLength - n.BitLength);
			}

			return trunc;
		}

		private ECDSASignature ToPositive(ECDSASignature sig)
		{
			return new ECDSASignature(new BouncyCastle.Math.BigInteger(1, sig.R.ToByteArray()), new BouncyCastle.Math.BigInteger(1, sig.S.ToByteArray()));
		}

		[Fact]
		[Trait("Core", "Core")]
		public void tx_valid()
		{
			// Read tests from test/data/tx_valid.json
			// Format is an array of arrays
			// Inner arrays are either [ "comment" ]
			// or [[[prevout hash, prevout index, prevout scriptPubKey], [input 2], ...],"], serializedTransaction, enforceP2SH
			// ... where all scripts are stringified scripts.
			var tests = TestCase.read_json("data/tx_valid.json");
			foreach(var test in tests)
			{
				string strTest = test.ToString();
				//Skip comments
				if(!(test[0] is JArray))
					continue;
				JArray inputs = (JArray)test[0];
				if(test.Count != 3 || !(test[1] is string) || !(test[2] is string))
				{
					Assert.False(true, "Bad test: " + strTest);
					continue;
				}

				Dictionary<OutPoint, Script> mapprevOutScriptPubKeys = new Dictionary<OutPoint, Script>();
				foreach(var vinput in inputs)
				{
					mapprevOutScriptPubKeys[new OutPoint(uint256.Parse(vinput[0].ToString()), int.Parse(vinput[1].ToString()))] = script_tests.ParseScript(vinput[2].ToString());
				}

				Transaction tx = Transaction.Parse((string)test[1]);
				ValidationState state = Network.Main.CreateValidationState();
				Assert.True(state.CheckTransaction(tx), strTest);
				Assert.True(state.IsValid);


				for(int i = 0 ; i < tx.Inputs.Count ; i++)
				{
					if(!mapprevOutScriptPubKeys.ContainsKey(tx.Inputs[i].PrevOut))
					{
						Assert.False(true, "Bad test: " + strTest);
						continue;
					}

					var valid = Script.VerifyScript(
						mapprevOutScriptPubKeys[tx.Inputs[i].PrevOut],
						tx,
						i,
						null,
						ParseFlags(test[2].ToString())
						, 0);
					Assert.True(valid, strTest + " failed");
				}
			}
		}

		ScriptVerify ParseFlags(string strFlags)
		{
			ScriptVerify flags = 0;
			var words = strFlags.Split(',');


			// Note how NOCACHE is not included as it is a runtime-only flag.
			Dictionary<string, ScriptVerify> mapFlagNames = new Dictionary<string, ScriptVerify>();
			if(mapFlagNames.Count == 0)
			{
				mapFlagNames["NONE"] = ScriptVerify.None;
				mapFlagNames["P2SH"] = ScriptVerify.P2SH;
				mapFlagNames["STRICTENC"] = ScriptVerify.StrictEnc;
				mapFlagNames["LOW_S"] = ScriptVerify.LowS;
				mapFlagNames["NULLDUMMY"] = ScriptVerify.NullDummy;
				mapFlagNames["CHECKLOCKTIMEVERIFY"] = ScriptVerify.CheckLockTimeVerify;
				mapFlagNames["CHECKSEQUENCEVERIFY"] = ScriptVerify.CheckSequenceVerify;
				mapFlagNames["DERSIG"] = ScriptVerify.DerSig;
			}

			foreach(string word in words)
			{
				if(!mapFlagNames.ContainsKey(word))
					Assert.False(true, "Bad test: unknown verification flag '" + word + "'");
				flags |= mapFlagNames[word];
			}

			return flags;
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CheckSequenceLocksAreCorrect()
		{
			//CheckSequenceLocksAreCorrectCore(new Sequence(f));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void SequenceStructParsedCorrectly()
		{
			Assert.True(new Sequence() == 0xFFFFFFFFU);
			Assert.False(new Sequence().IsRelativeLock);
			Assert.False(new Sequence().IsRBF);

			Assert.True(new Sequence(1) == 1U);
			Assert.True(new Sequence(1).IsRelativeLock);
			Assert.True(new Sequence(1).IsRBF);
			Assert.True(new Sequence(1).LockType == SequenceLockType.Height);
			Assert.True(new Sequence(1) == 1U);
			Assert.True(new Sequence(1).LockHeight == 1);
			Assert.Throws<InvalidOperationException>(() => new Sequence(1).LockPeriod);

			Assert.True(new Sequence(0xFFFF).LockHeight == 0xFFFF);
			Assert.Throws<ArgumentOutOfRangeException>(() => new Sequence(0xFFFF + 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => new Sequence(-1));

			var time = TimeSpan.FromSeconds(512 * 0xFF);
			Assert.True(new Sequence(time) == (uint)(0xFF | 1 << 22));
			Assert.True(new Sequence(time).IsRelativeLock);
			Assert.True(new Sequence(time).IsRBF);
			Assert.Throws<ArgumentOutOfRangeException>(() => new Sequence(TimeSpan.FromSeconds(512 * (0xFFFF + 1))));
			new Sequence(TimeSpan.FromSeconds(512 * (0xFFFF)));
			Assert.Throws<InvalidOperationException>(() => new Sequence(time).LockHeight);
		}

		[Fact]
		[Trait("Core", "Core")]
		public void tx_invalid()
		{
			// Read tests from test/data/tx_valid.json
			// Format is an array of arrays
			// Inner arrays are either [ "comment" ]
			// or [[[prevout hash, prevout index, prevout scriptPubKey], [input 2], ...],"], serializedTransaction, enforceP2SH
			// ... where all scripts are stringified scripts.
			var tests = TestCase.read_json("data/tx_invalid.json");
			string comment = null;
			foreach(var test in tests)
			{
				string strTest = test.ToString();
				//Skip comments
				if(!(test[0] is JArray))
				{
					comment = test[0].ToString();
					continue;
				}
				JArray inputs = (JArray)test[0];
				if(test.Count != 3 || !(test[1] is string) || !(test[2] is string))
				{
					Assert.False(true, "Bad test: " + strTest);
					continue;
				}

				Dictionary<OutPoint, Script> mapprevOutScriptPubKeys = new Dictionary<OutPoint, Script>();
				foreach(var vinput in inputs)
				{
					mapprevOutScriptPubKeys[new OutPoint(uint256.Parse(vinput[0].ToString()), int.Parse(vinput[1].ToString()))] = script_tests.ParseScript(vinput[2].ToString());
				}

				Transaction tx = Transaction.Parse((string)test[1]);

				ValidationState state = Network.Main.CreateValidationState();
				var fValid = state.CheckTransaction(tx) && state.IsValid;

				for(int i = 0 ; i < tx.Inputs.Count && fValid ; i++)
				{
					if(!mapprevOutScriptPubKeys.ContainsKey(tx.Inputs[i].PrevOut))
					{
						Assert.False(true, "Bad test: " + strTest);
						continue;
					}

					fValid = Script.VerifyScript(
					   mapprevOutScriptPubKeys[tx.Inputs[i].PrevOut],
					   tx,
					   i,
					   null,
					   ParseFlags(test[2].ToString())
					   , 0);
				}
				Assert.True(!fValid, strTest + " failed");
			}


		}

		[Fact]
		[Trait("Core", "Core")]
		public void basic_transaction_tests()
		{
			// Random real transaction (e2769b09e784f32f62ef849763d4f45b98e07ba658647343b915ff832b110436)
			var ch = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01, 0x6b, 0xff, 0x7f, 0xcd, 0x4f, 0x85, 0x65, 0xef, 0x40, 0x6d, 0xd5, 0xd6, 0x3d, 0x4f, 0xf9, 0x4f, 0x31, 0x8f, 0xe8, 0x20, 0x27, 0xfd, 0x4d, 0xc4, 0x51, 0xb0, 0x44, 0x74, 0x01, 0x9f, 0x74, 0xb4, 0x00, 0x00, 0x00, 0x00, 0x8c, 0x49, 0x30, 0x46, 0x02, 0x21, 0x00, 0xda, 0x0d, 0xc6, 0xae, 0xce, 0xfe, 0x1e, 0x06, 0xef, 0xdf, 0x05, 0x77, 0x37, 0x57, 0xde, 0xb1, 0x68, 0x82, 0x09, 0x30, 0xe3, 0xb0, 0xd0, 0x3f, 0x46, 0xf5, 0xfc, 0xf1, 0x50, 0xbf, 0x99, 0x0c, 0x02, 0x21, 0x00, 0xd2, 0x5b, 0x5c, 0x87, 0x04, 0x00, 0x76, 0xe4, 0xf2, 0x53, 0xf8, 0x26, 0x2e, 0x76, 0x3e, 0x2d, 0xd5, 0x1e, 0x7f, 0xf0, 0xbe, 0x15, 0x77, 0x27, 0xc4, 0xbc, 0x42, 0x80, 0x7f, 0x17, 0xbd, 0x39, 0x01, 0x41, 0x04, 0xe6, 0xc2, 0x6e, 0xf6, 0x7d, 0xc6, 0x10, 0xd2, 0xcd, 0x19, 0x24, 0x84, 0x78, 0x9a, 0x6c, 0xf9, 0xae, 0xa9, 0x93, 0x0b, 0x94, 0x4b, 0x7e, 0x2d, 0xb5, 0x34, 0x2b, 0x9d, 0x9e, 0x5b, 0x9f, 0xf7, 0x9a, 0xff, 0x9a, 0x2e, 0xe1, 0x97, 0x8d, 0xd7, 0xfd, 0x01, 0xdf, 0xc5, 0x22, 0xee, 0x02, 0x28, 0x3d, 0x3b, 0x06, 0xa9, 0xd0, 0x3a, 0xcf, 0x80, 0x96, 0x96, 0x8d, 0x7d, 0xbb, 0x0f, 0x91, 0x78, 0xff, 0xff, 0xff, 0xff, 0x02, 0x8b, 0xa7, 0x94, 0x0e, 0x00, 0x00, 0x00, 0x00, 0x19, 0x76, 0xa9, 0x14, 0xba, 0xde, 0xec, 0xfd, 0xef, 0x05, 0x07, 0x24, 0x7f, 0xc8, 0xf7, 0x42, 0x41, 0xd7, 0x3b, 0xc0, 0x39, 0x97, 0x2d, 0x7b, 0x88, 0xac, 0x40, 0x94, 0xa8, 0x02, 0x00, 0x00, 0x00, 0x00, 0x19, 0x76, 0xa9, 0x14, 0xc1, 0x09, 0x32, 0x48, 0x3f, 0xec, 0x93, 0xed, 0x51, 0xf5, 0xfe, 0x95, 0xe7, 0x25, 0x59, 0xf2, 0xcc, 0x70, 0x43, 0xf9, 0x88, 0xac, 0x00, 0x00, 0x00, 0x00, 0x00 };
			var vch = ch.Take(ch.Length - 1).ToArray();

			Transaction tx = new Transaction(vch);
			ValidationState state = Network.Main.CreateValidationState();
			Assert.True(state.CheckTransaction(tx) && state.IsValid, "Simple deserialized transaction should be valid.");

			// Check that duplicate txins fail
			tx.Inputs.Add(tx.Inputs[0]);
			Assert.True(!state.CheckTransaction(tx) || !state.IsValid, "Transaction with duplicate txins should be invalid.");
		}



		[Fact]
		[Trait("Core", "Core")]
		public void test_Get()
		{
			byte[] dummyPubKey = new byte[65];
			dummyPubKey[0] = 0x04;

			byte[] dummyPubKey2 = new byte[33];
			dummyPubKey2[0] = 0x02;
			//CBasicKeyStore keystore;
			//CCoinsView coinsDummy;
			CoinsView coins = new CoinsView();//(coinsDummy);
			Transaction[] dummyTransactions = SetupDummyInputs(coins);//(keystore, coins);

			Transaction t1 = new Transaction();
			t1.Inputs.AddRange(Enumerable.Range(0, 3).Select(_ => new TxIn()));
			t1.Inputs[0].PrevOut.Hash = dummyTransactions[0].GetHash();
			t1.Inputs[0].PrevOut.N = 1;
			t1.Inputs[0].ScriptSig += dummyPubKey;
			t1.Inputs[1].PrevOut.Hash = dummyTransactions[1].GetHash();
			t1.Inputs[1].PrevOut.N = 0;
			t1.Inputs[1].ScriptSig = t1.Inputs[1].ScriptSig + dummyPubKey + dummyPubKey2;
			t1.Inputs[2].PrevOut.Hash = dummyTransactions[1].GetHash();
			t1.Inputs[2].PrevOut.N = 1;
			t1.Inputs[2].ScriptSig = t1.Inputs[2].ScriptSig + dummyPubKey + dummyPubKey2;
			t1.Outputs.AddRange(Enumerable.Range(0, 2).Select(_ => new TxOut()));
			t1.Outputs[0].Value = 90 * Money.CENT;
			t1.Outputs[0].ScriptPubKey += OpcodeType.OP_1;

			Assert.True(StandardScripts.AreInputsStandard(t1, coins));
			//Assert.Equal(coins.GetValueIn(t1), (50+21+22)*Money.CENT);

			//// Adding extra junk to the scriptSig should make it non-standard:
			t1.Inputs[0].ScriptSig += OpcodeType.OP_11;
			Assert.True(!StandardScripts.AreInputsStandard(t1, coins));

			//// ... as should not having enough:
			t1.Inputs[0].ScriptSig = new Script();
			Assert.True(!StandardScripts.AreInputsStandard(t1, coins));
		}

		private Transaction[] SetupDummyInputs(CoinsView coinsRet)
		{
			Transaction[] dummyTransactions = Enumerable.Range(0, 2).Select(_ => new Transaction()).ToArray();

			// Add some keys to the keystore:
			Key[] key = Enumerable.Range(0, 4).Select((_, i) => new Key(i % 2 != 0)).ToArray();


			// Create some dummy input transactions
			dummyTransactions[0].Outputs.AddRange(Enumerable.Range(0, 2).Select(_ => new TxOut()));
			dummyTransactions[0].Outputs[0].Value = 11 * Money.CENT;
			dummyTransactions[0].Outputs[0].ScriptPubKey = dummyTransactions[0].Outputs[0].ScriptPubKey + key[0].PubKey.ToBytes() + OpcodeType.OP_CHECKSIG;
			dummyTransactions[0].Outputs[1].Value = 50 * Money.CENT;
			dummyTransactions[0].Outputs[1].ScriptPubKey = dummyTransactions[0].Outputs[1].ScriptPubKey + key[1].PubKey.ToBytes() + OpcodeType.OP_CHECKSIG;
			coinsRet.AddTransaction(dummyTransactions[0], 0);


			dummyTransactions[1].Outputs.AddRange(Enumerable.Range(0, 2).Select(_ => new TxOut()));
			dummyTransactions[1].Outputs[0].Value = 21 * Money.CENT;
			dummyTransactions[1].Outputs[0].ScriptPubKey = key[2].PubKey.GetAddress(Network.Main).ScriptPubKey;
			dummyTransactions[1].Outputs[1].Value = 22 * Money.CENT;
			dummyTransactions[1].Outputs[1].ScriptPubKey = key[3].PubKey.GetAddress(Network.Main).ScriptPubKey;
			coinsRet.AddTransaction(dummyTransactions[1], 0);


			return dummyTransactions;
		}

		class CKeyStore
		{
			internal List<Tuple<Key, PubKey>> _Keys = new List<Tuple<Key, PubKey>>();
			internal List<Script> _Scripts = new List<Script>();
			internal void AddKeyPubKey(Key key, PubKey pubkey)
			{
				_Keys.Add(Tuple.Create(key, pubkey));
			}

			internal void RemoveKeyPubKey(Key key)
			{
				_Keys.Remove(_Keys.First(o => o.Item1 == key));
			}

			internal void AddCScript(Script scriptPubkey)
			{
				_Scripts.Add(scriptPubkey);
			}
		}

		void CreateCreditAndSpend(CKeyStore keystore, Script outscript, ref Transaction output, ref Transaction input, bool success = true)
		{
			Transaction outputm = new Transaction();
			outputm.Version = 1;
			outputm.Inputs.Add(new TxIn());
			outputm.Inputs[0].PrevOut = new OutPoint();
			outputm.Inputs[0].ScriptSig = Script.Empty;
			outputm.Inputs[0].WitScript = new WitScript();
			outputm.Outputs.Add(new TxOut());
			outputm.Outputs[0].Value = Money.Satoshis(1);
			outputm.Outputs[0].ScriptPubKey = outscript;

			output = outputm.Clone();

			Assert.True(output.Inputs.Count == 1);
			Assert.True(output.Inputs[0].ToBytes().SequenceEqual(outputm.Inputs[0].ToBytes()));
			Assert.True(output.Outputs.Count == 1);
			Assert.True(output.Inputs[0].ToBytes().SequenceEqual(outputm.Inputs[0].ToBytes()));
			Assert.True(!output.HasWitness);

			Transaction inputm = new Transaction();
			inputm.Version = 1;
			inputm.Inputs.Add(new TxIn());
			inputm.Inputs[0].PrevOut.Hash = output.GetHash();
			inputm.Inputs[0].PrevOut.N = 0;
			inputm.Inputs[0].WitScript = new WitScript();
			inputm.Outputs.Add(new TxOut());
			inputm.Outputs[0].Value = Money.Satoshis(1);
			inputm.Outputs[0].ScriptPubKey = Script.Empty;
			bool ret = SignSignature(keystore, output, inputm, 0);
			Assert.True(ret == success);
			input = inputm.Clone();
			Assert.True(input.Inputs.Count == 1);
			Assert.True(input.Inputs[0].ToBytes().SequenceEqual(inputm.Inputs[0].ToBytes()));
			Assert.True(input.Outputs.Count == 1);
			Assert.True(input.Outputs[0].ToBytes().SequenceEqual(inputm.Outputs[0].ToBytes()));
			if(!inputm.HasWitness)
			{
				Assert.True(!input.HasWitness);
			}
			else
			{
				Assert.True(input.HasWitness);
				Assert.True(input.Inputs[0].WitScript.ToBytes().SequenceEqual(inputm.Inputs[0].WitScript.ToBytes()));
			}
		}

		private bool SignSignature(CKeyStore keystore, Transaction txFrom, Transaction txTo, int nIn)
		{
			var builder = CreateBuilder(keystore, txFrom);
			builder.SignTransactionInPlace(txTo);
			return builder.Verify(txTo);
		}

		private void CombineSignatures(CKeyStore keystore, Transaction txFrom, ref Transaction input1, Transaction input2)
		{
			var builder = CreateBuilder(keystore, txFrom);
			input1 = builder.CombineSignatures(input1, input2);
		}

		private static TransactionBuilder CreateBuilder(CKeyStore keystore, Transaction txFrom)
		{
			var coins = txFrom.Outputs.AsCoins().ToArray();
			var builder = new TransactionBuilder()
			{
				StandardTransactionPolicy = new StandardTransactionPolicy()
				{
					CheckFee = false,
					MinRelayTxFee = null,
					UseConsensusLib = false,
					CheckScriptPubKey = false
				}
			}
			.AddCoins(coins)
			.AddKeys(keystore._Keys.Select(k => k.Item1).ToArray())
			.AddKnownRedeems(keystore._Scripts.ToArray());
			return builder;
		}

		void CheckWithFlag(Transaction output, Transaction input, ScriptVerify flags, bool success)
		{
			Transaction inputi = input.Clone();
			ScriptEvaluationContext ctx = new ScriptEvaluationContext();
			ctx.ScriptVerify = flags;
			bool ret = ctx.VerifyScript(inputi.Inputs[0].ScriptSig, output.Outputs[0].ScriptPubKey, new TransactionChecker(inputi, 0, output.Outputs[0].Value));
			Assert.True(ret == success);
		}

		static Script PushAll(ContextStack<byte[]> values)
		{
			List<Op> result = new List<Op>();
			foreach(var v in values.Reverse())
			{
				if(v.Length == 0)
				{
					result.Add(OpcodeType.OP_0);
				}
				else
				{
					result.Add(Op.GetPushOp(v));
				}
			}
			return new Script(result.ToArray());
		}

		void ReplaceRedeemScript(TxIn input, Script redeemScript)
		{
			ScriptEvaluationContext ctx = new ScriptEvaluationContext();
			ctx.ScriptVerify = ScriptVerify.StrictEnc;
			ctx.EvalScript(input.ScriptSig, new Transaction(), 0);
			var stack = ctx.Stack;
			Assert.True(stack.Count > 0);
			stack.Pop();
			stack.Push(redeemScript.ToBytes());
			input.ScriptSig = PushAll(stack);
		}

		[Fact]
		[Trait("Core", "Core")]
		public void test_witness()
		{
			CKeyStore keystore = new CKeyStore();
			CKeyStore keystore2 = new CKeyStore();
			var key1 = new Key(true);
			var key2 = new Key(true);
			var key3 = new Key(true);
			var key1L = new Key(false);
			var key2L = new Key(false);
			var pubkey1 = key1.PubKey;
			var pubkey2 = key2.PubKey;
			var pubkey3 = key3.PubKey;
			var pubkey1L = key1L.PubKey;
			var pubkey2L = key2L.PubKey;
			keystore.AddKeyPubKey(key1, pubkey1);
			keystore.AddKeyPubKey(key2, pubkey2);
			keystore.AddKeyPubKey(key1L, pubkey1L);
			keystore.AddKeyPubKey(key2L, pubkey2L);
			Script scriptPubkey1, scriptPubkey2, scriptPubkey1L, scriptPubkey2L, scriptMulti;
			scriptPubkey1 = new Script(Op.GetPushOp(pubkey1.ToBytes()), OpcodeType.OP_CHECKSIG);
			scriptPubkey2 = new Script(Op.GetPushOp(pubkey2.ToBytes()), OpcodeType.OP_CHECKSIG);
			scriptPubkey1L = new Script(Op.GetPushOp(pubkey1L.ToBytes()), OpcodeType.OP_CHECKSIG);
			scriptPubkey2L = new Script(Op.GetPushOp(pubkey2L.ToBytes()), OpcodeType.OP_CHECKSIG);
			List<PubKey> oneandthree = new List<PubKey>();
			oneandthree.Add(pubkey1);
			oneandthree.Add(pubkey3);
			scriptMulti = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, oneandthree.ToArray());
			keystore.AddCScript(scriptPubkey1);
			keystore.AddCScript(scriptPubkey2);
			keystore.AddCScript(scriptPubkey1L);
			keystore.AddCScript(scriptPubkey2L);
			keystore.AddCScript(scriptMulti);
			keystore.AddCScript(GetScriptForWitness(scriptPubkey1));
			keystore.AddCScript(GetScriptForWitness(scriptPubkey2));
			keystore.AddCScript(GetScriptForWitness(scriptPubkey1L));
			keystore.AddCScript(GetScriptForWitness(scriptPubkey2L));
			keystore.AddCScript(GetScriptForWitness(scriptMulti));
			keystore2.AddCScript(scriptMulti);
			keystore2.AddCScript(GetScriptForWitness(scriptMulti));
			keystore2.AddKeyPubKey(key3, pubkey3);

			Transaction output1, output2;
			output1 = new Transaction();
			output2 = new Transaction();
			Transaction input1, input2;
			input1 = new Transaction();
			input2 = new Transaction();

			// Normal pay-to-compressed-pubkey.
			CreateCreditAndSpend(keystore, scriptPubkey1, ref output1, ref input1);
			CreateCreditAndSpend(keystore, scriptPubkey2, ref output2, ref input2);
			CheckWithFlag(output1, input1, 0, true);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Witness | ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);
			CheckWithFlag(output1, input2, 0, false);
			CheckWithFlag(output1, input2, ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Witness | ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Standard, false);

			// P2SH pay-to-compressed-pubkey.
			CreateCreditAndSpend(keystore, scriptPubkey1.Hash.ScriptPubKey, ref output1, ref input1);
			CreateCreditAndSpend(keystore, scriptPubkey2.Hash.ScriptPubKey, ref output2, ref input2);
			ReplaceRedeemScript(input2.Inputs[0], scriptPubkey1);
			CheckWithFlag(output1, input1, 0, true);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Witness | ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);
			CheckWithFlag(output1, input2, 0, true);
			CheckWithFlag(output1, input2, ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Witness | ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Standard, false);

			// Witness pay-to-compressed-pubkey (v0).
			CreateCreditAndSpend(keystore, GetScriptForWitness(scriptPubkey1), ref output1, ref input1);
			CreateCreditAndSpend(keystore, GetScriptForWitness(scriptPubkey2), ref output2, ref input2);
			CheckWithFlag(output1, input1, 0, true);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Witness | ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);
			CheckWithFlag(output1, input2, 0, true);
			CheckWithFlag(output1, input2, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input2, ScriptVerify.Witness | ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Standard, false);

			// P2SH witness pay-to-compressed-pubkey (v0).
			CreateCreditAndSpend(keystore, GetScriptForWitness(scriptPubkey1).Hash.ScriptPubKey, ref output1, ref input1);
			CreateCreditAndSpend(keystore, GetScriptForWitness(scriptPubkey2).Hash.ScriptPubKey, ref output2, ref input2);
			ReplaceRedeemScript(input2.Inputs[0], GetScriptForWitness(scriptPubkey1));
			CheckWithFlag(output1, input1, 0, true);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Witness | ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);
			CheckWithFlag(output1, input2, 0, true);
			CheckWithFlag(output1, input2, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input2, ScriptVerify.Witness | ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Standard, false);

			// Normal pay-to-uncompressed-pubkey.
			CreateCreditAndSpend(keystore, scriptPubkey1L, ref output1, ref input1);
			CreateCreditAndSpend(keystore, scriptPubkey2L, ref output2, ref input2);
			CheckWithFlag(output1, input1, 0, true);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Witness | ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);
			CheckWithFlag(output1, input2, 0, false);
			CheckWithFlag(output1, input2, ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Witness | ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Standard, false);

			// P2SH pay-to-uncompressed-pubkey.
			CreateCreditAndSpend(keystore, scriptPubkey1L.Hash.ScriptPubKey, ref output1, ref input1);
			CreateCreditAndSpend(keystore, scriptPubkey2L.Hash.ScriptPubKey, ref output2, ref input2);
			ReplaceRedeemScript(input2.Inputs[0], scriptPubkey1L);
			CheckWithFlag(output1, input1, 0, true);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Witness | ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);
			CheckWithFlag(output1, input2, 0, true);
			CheckWithFlag(output1, input2, ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Witness | ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Standard, false);

			// Witness pay-to-uncompressed-pubkey (v1).
			CreateCreditAndSpend(keystore, GetScriptForWitness(scriptPubkey1L), ref output1, ref input1);
			CreateCreditAndSpend(keystore, GetScriptForWitness(scriptPubkey2L), ref output2, ref input2);
			CheckWithFlag(output1, input1, 0, true);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Witness | ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);
			CheckWithFlag(output1, input2, 0, true);
			CheckWithFlag(output1, input2, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input2, ScriptVerify.Witness | ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Standard, false);

			// P2SH witness pay-to-uncompressed-pubkey (v1).
			CreateCreditAndSpend(keystore, GetScriptForWitness(scriptPubkey1L).Hash.ScriptPubKey, ref output1, ref input1);
			CreateCreditAndSpend(keystore, GetScriptForWitness(scriptPubkey2L).Hash.ScriptPubKey, ref output2, ref input2);
			ReplaceRedeemScript(input2.Inputs[0], GetScriptForWitness(scriptPubkey1L));
			CheckWithFlag(output1, input1, 0, true);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Witness | ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);
			CheckWithFlag(output1, input2, 0, true);
			CheckWithFlag(output1, input2, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input2, ScriptVerify.Witness | ScriptVerify.P2SH, false);
			CheckWithFlag(output1, input2, ScriptVerify.Standard, false);

			// Normal 2-of-2 multisig
			CreateCreditAndSpend(keystore, scriptMulti, ref output1, ref input1, false);
			CheckWithFlag(output1, input1, 0, false);
			CreateCreditAndSpend(keystore2, scriptMulti, ref output2, ref input2, false);
			CheckWithFlag(output2, input2, 0, false);
			Assert.True(output1.ToBytes().SequenceEqual(output2.ToBytes()));
			CombineSignatures(keystore, output1, ref input1, input2);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);

			// P2SH 2-of-2 multisig
			CreateCreditAndSpend(keystore, scriptMulti.Hash.ScriptPubKey, ref output1, ref input1, false);
			CheckWithFlag(output1, input1, 0, true);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH, false);
			CreateCreditAndSpend(keystore2, scriptMulti.Hash.ScriptPubKey, ref output2, ref input2, false);
			CheckWithFlag(output2, input2, 0, true);
			CheckWithFlag(output2, input2, ScriptVerify.P2SH, false);
			Assert.True(output1.ToBytes().SequenceEqual(output2.ToBytes()));
			CombineSignatures(keystore, output1, ref input1, input2);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);

			// Witness 2-of-2 multisig
			CreateCreditAndSpend(keystore, GetScriptForWitness(scriptMulti), ref output1, ref input1, false);
			CheckWithFlag(output1, input1, 0, true);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH | ScriptVerify.Witness, false);
			CreateCreditAndSpend(keystore2, GetScriptForWitness(scriptMulti), ref output2, ref input2, false);
			CheckWithFlag(output2, input2, 0, true);
			CheckWithFlag(output2, input2, ScriptVerify.P2SH | ScriptVerify.Witness, false);
			Assert.True(output1.ToBytes().SequenceEqual(output2.ToBytes()));
			CombineSignatures(keystore, output1, ref input1, input2);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH | ScriptVerify.Witness, true);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);

			// P2SH witness 2-of-2 multisig
			CreateCreditAndSpend(keystore, GetScriptForWitness(scriptMulti).Hash.ScriptPubKey, ref output1, ref input1, false);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH, true);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH | ScriptVerify.Witness, false);
			CreateCreditAndSpend(keystore2, GetScriptForWitness(scriptMulti).Hash.ScriptPubKey, ref output2, ref input2, false);
			CheckWithFlag(output2, input2, ScriptVerify.P2SH, true);
			CheckWithFlag(output2, input2, ScriptVerify.P2SH | ScriptVerify.Witness, false);
			Assert.True(output1.ToBytes().SequenceEqual(output2.ToBytes()));
			CombineSignatures(keystore, output1, ref input1, input2);
			CheckWithFlag(output1, input1, ScriptVerify.P2SH | ScriptVerify.Witness, true);
			CheckWithFlag(output1, input1, ScriptVerify.Standard, true);
		}


		private Script GetScriptForWitness(Script scriptPubKey)
		{
			var pubkey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey);
			if(pubkey != null)
				return new Script(OpcodeType.OP_0, Op.GetPushOp(pubkey.Hash.ToBytes()));
			var pkh = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey);
			if(pkh != null)
				return new Script(OpcodeType.OP_0, Op.GetPushOp(pkh.ToBytes()));

			return new Script(OpcodeType.OP_0, Op.GetPushOp(scriptPubKey.WitHash.ToBytes()));
		}


		[Fact]
		[Trait("Core", "Core")]
		public void test_IsStandard()
		{
			var coins = new CoinsView();
			Transaction[] dummyTransactions = SetupDummyInputs(coins);

			Transaction t = new Transaction();
			t.Inputs.Add(new TxIn());
			t.Inputs[0].PrevOut.Hash = dummyTransactions[0].GetHash();
			t.Inputs[0].PrevOut.N = 1;
			t.Inputs[0].ScriptSig = new Script(Op.GetPushOp(new byte[65]));
			t.Outputs.Add(new TxOut());
			t.Outputs[0].Value = 90 * Money.CENT;
			Key key = new Key(true);
			t.Outputs[0].ScriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(key.PubKey.Hash);

			Assert.True(StandardScripts.IsStandardTransaction(t));

			t.Outputs[0].Value = 501; //dust
			Assert.True(!StandardScripts.IsStandardTransaction(t));

			t.Outputs[0].Value = 2730; // not dust
			Assert.True(StandardScripts.IsStandardTransaction(t));

			t.Outputs[0].ScriptPubKey = new Script() + OpcodeType.OP_1;
			Assert.True(!StandardScripts.IsStandardTransaction(t));

			// 80-byte TX_NULL_DATA (standard)
			t.Outputs[0].ScriptPubKey = new Script() + OpcodeType.OP_RETURN + ParseHex("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef3804678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38");
			Assert.True(StandardScripts.IsStandardTransaction(t));

			// 81-byte TX_NULL_DATA (non-standard)
			t.Outputs[0].ScriptPubKey = new Script() + OpcodeType.OP_RETURN + ParseHex("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef3804678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef3800");
			Assert.True(!StandardScripts.IsStandardTransaction(t));

			// TX_NULL_DATA w/o PUSHDATA
			t.Outputs.Clear();
			t.Outputs.Add(new TxOut());
			t.Outputs[0].ScriptPubKey = new Script() + OpcodeType.OP_RETURN;
			Assert.True(StandardScripts.IsStandardTransaction(t));

			// Only one TX_NULL_DATA permitted in all cases
			t.Outputs.Clear();
			t.Outputs.Add(new TxOut());
			t.Outputs.Add(new TxOut());
			t.Outputs[0].ScriptPubKey = new Script() + OpcodeType.OP_RETURN + ParseHex("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38");
			t.Outputs[1].ScriptPubKey = new Script() + OpcodeType.OP_RETURN + ParseHex("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38");
			Assert.True(!StandardScripts.IsStandardTransaction(t));

			t.Outputs[0].ScriptPubKey = new Script() + OpcodeType.OP_RETURN + ParseHex("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38");
			t.Outputs[1].ScriptPubKey = new Script() + OpcodeType.OP_RETURN;
			Assert.True(!StandardScripts.IsStandardTransaction(t));

			t.Outputs[0].ScriptPubKey = new Script() + OpcodeType.OP_RETURN;
			t.Outputs[1].ScriptPubKey = new Script() + OpcodeType.OP_RETURN;
			Assert.True(!StandardScripts.IsStandardTransaction(t));
		}

		private byte[] ParseHex(string data)
		{
			return Encoders.Hex.DecodeData(data);
		}
	}
}
