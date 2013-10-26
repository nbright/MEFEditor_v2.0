﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Windows;

using Drawing;

using UnitTesting.Drawing_TestUtils;

namespace UnitTesting
{
    [TestClass]
    public class SceneNavigator_Testing
    {
        [TestMethod]
        public void Scene_ItemIntersection()
        {

            DrawingTest.Create
                .Item("A", 0, 0)
                .Item("B", 500, 0)
                .Item("C", 1000, 0)

                .AssertIntersection(
                    new Point(50, 50), //Point inside A
                    new Point(1050, 50), //Point inside C
                    "B"
                )
                ;
        }

        [TestMethod]
        public void Scene_TargetIntersection()
        {

            DrawingTest.Create
                .Item("A", 0, 0)
                .Item("B", 200, 500)
                .Item("C", 0, 1000)

                .AssertIntersection(
                    new Point(50, 50), //Point inside A
                    new Point(50, 1050), //Point inside C
                    "C" //no obstacle is hitted
                )
                ;
        }

        [TestMethod]
        public void Scene_DiagonalIntersection()
        {

            DrawingTest.Create
                .Item("A", 0, 0)
                .Item("B", 500, 0)
                .Item("C", 1000, 50)

                .AssertIntersection(
                    new Point(50, 0), //Point inside A
                    new Point(1050, 100), //Point inside C
                    "B" //B obstacle is hitted
                )
                ;
        }
    }
}